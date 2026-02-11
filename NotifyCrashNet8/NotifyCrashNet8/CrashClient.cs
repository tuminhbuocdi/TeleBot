using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;
using NotifyCrashNet8.Data;

public class CrashClient
{
    ClientWebSocket ws;
    Uri uri = new Uri("wss://socketv4.bcgame49.com/socket.io/?EIO=3&transport=websocket");

    private readonly IServiceScopeFactory _scopeFactory;

    decimal lastCrash = 0;
    int under2 = 0;

    public CrashClient(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Start(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                ws = new ClientWebSocket();
                ws.Options.SetRequestHeader("Origin", "https://bc.game");

                Console.WriteLine("Connecting...");
                await ws.ConnectAsync(uri, cancellationToken);

                Console.WriteLine("Connected");

                await Send("40"); // join socket.io

                await ReceiveLoop(cancellationToken);
            }
            catch (Exception ex)
            {
                Console.WriteLine("Reconnect in 3s: " + ex.Message);
                await Task.Delay(3000, cancellationToken);
            }
        }
    }

    async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];

        while (ws.State == WebSocketState.Open)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

            string msg = Encoding.UTF8.GetString(buffer, 0, result.Count);

            // ping pong
            if (msg == "3")
            {
                await Send("3");
                continue;
            }

            if (msg.Contains("crash") && msg.Contains("result"))
            {
                try
                {
                    string jsonText = ExtractJson(msg);
                    if (jsonText == null) continue;

                    var json = JObject.Parse(jsonText);
                    decimal crash = json["result"]["crash"].Value<decimal>();

                    if (crash != lastCrash)
                    {
                        lastCrash = crash;
                        await Analyze(crash, cancellationToken);
                    }
                }
                catch { }
            }
        }
    }

    async Task Send(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, CancellationToken.None);
    }

    string ExtractJson(string input)
    {
        var m = Regex.Match(input, @"\{.*\}");
        if (m.Success) return m.Value;
        return null;
    }

    async Task Analyze(decimal crash, CancellationToken cancellationToken)
    {
        Console.WriteLine("Crash: x" + crash);

        using (var scope = _scopeFactory.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.CrashEvents.Add(new CrashEvent
            {
                CreatedAtUtc = DateTime.UtcNow,
                Crash = crash,
            });
            await db.SaveChangesAsync(cancellationToken);
        }

        if (crash < 2) under2++;
        else under2 = 0;

        if (under2 >= 5)
        {
            await Telegram.Send("🔥 5 round dưới x2");
            under2 = 0;
        }

        if (crash >= 10)
        {
            await Telegram.Send("🚀 High crash x" + crash);
        }
    }
}
