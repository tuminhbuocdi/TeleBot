using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NotifyCrashNet8;
using NotifyCrashNet8.Data;
using NotifyCrashNet8.Services;

internal sealed class Program
{
    private static async Task Main(string[] args)
    {
        Console.WriteLine("BC Crash tool started");

        using IHost host = Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                services.AddDbContext<AppDbContext>(options =>
                    options.UseSqlServer(context.Configuration.GetConnectionString("Default")));

                services.AddHttpClient(nameof(BcGameHistoryPoller));
                services.AddSingleton<CrashRecordProcService>();
                services.AddHostedService<BcGameHistoryPoller>();
            })
            .Build();

        using (var scope = host.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            bool ok = await db.Database.CanConnectAsync();
            Console.WriteLine(ok ? "SQL Server: Connected" : "SQL Server: Cannot connect");
        }

        await host.RunAsync();
    }
}
