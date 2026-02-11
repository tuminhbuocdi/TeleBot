using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using NotifyCrashNet8.Data;

namespace NotifyCrashNet8.Services;

public sealed class BcGameHistoryPoller : BackgroundService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BcGameHistoryPoller> _logger;
    private readonly IConfiguration _configuration;

    public BcGameHistoryPoller(
        IHttpClientFactory httpClientFactory,
        IServiceScopeFactory scopeFactory,
        ILogger<BcGameHistoryPoller> logger,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _configuration = configuration;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollOnce(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Poll failed");
            }

            int intervalSeconds = _configuration.GetValue<int?>("BcGame:IntervalSeconds") ?? 20;
            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task PollOnce(CancellationToken cancellationToken)
    {
        string? url = _configuration["BcGame:Url"];
        if (string.IsNullOrWhiteSpace(url))
        {
            _logger.LogWarning("BcGame:Url is empty");
            return;
        }

        string? cookie = _configuration["BcGame:Cookie"];
        int page = _configuration.GetValue<int?>("BcGame:Page") ?? 1;
        int pageSize = _configuration.GetValue<int?>("BcGame:PageSize") ?? 50;
        int maxRows = _configuration.GetValue<int?>("BcGame:MaxRows") ?? 50000;

        var payload = new
        {
            gameUrl = "crash",
            page,
            pageSize
        };

        string json = JsonConvert.SerializeObject(payload);

        using var request = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };

        request.Headers.TryAddWithoutValidation("origin", "https://bcgame49.com");
        if (!string.IsNullOrWhiteSpace(cookie))
        {
            request.Headers.TryAddWithoutValidation("Cookie", cookie);
        }

        var client = _httpClientFactory.CreateClient(nameof(BcGameHistoryPoller));

        using var response = await client.SendAsync(request, cancellationToken);
        string raw = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("HTTP {StatusCode}: {Body}", (int)response.StatusCode, raw);
            return;
        }

        var data = JsonConvert.DeserializeObject<BcGameResponse>(raw);
        if (data == null || data.Code != 0 || data.Data?.List == null)
        {
            _logger.LogWarning("Unexpected response: {Body}", raw);
            return;
        }

        var inputs = new List<CrashRecordInput>(data.Data.List.Count);
        foreach (var item in data.Data.List)
        {
            if (!long.TryParse(item.GameId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var gameId))
            {
                continue;
            }

            if (!decimal.TryParse(item.Rate, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                continue;
            }

            inputs.Add(new CrashRecordInput
            {
                GameId = gameId,
                Rate = rate,
            });
        }

        using (var scope = _scopeFactory.CreateScope())
        {
            var proc = scope.ServiceProvider.GetRequiredService<CrashRecordProcService>();
            await proc.UpsertAndTrimAsync(inputs, maxRows, cancellationToken);
        }

        _logger.LogInformation("Synced CrashRecords: {Count}", inputs.Count);
    }

    private sealed class BcGameResponse
    {
        [JsonProperty("code")]
        public int Code { get; set; }

        [JsonProperty("data")]
        public BcGameData? Data { get; set; }
    }

    private sealed class BcGameData
    {
        [JsonProperty("list")]
        public List<BcGameItem>? List { get; set; }
    }

    private sealed class BcGameItem
    {
        [JsonProperty("gameId")]
        public string? GameId { get; set; }

        [JsonProperty("gameDetail")]
        public string? GameDetail { get; set; }

        [JsonIgnore]
        public string? Rate
        {
            get
            {
                if (string.IsNullOrWhiteSpace(GameDetail)) return null;

                try
                {
                    var detail = JsonConvert.DeserializeObject<GameDetailObj>(GameDetail);
                    return detail?.Rate;
                }
                catch
                {
                    return null;
                }
            }
        }

        private sealed class GameDetailObj
        {
            [JsonProperty("rate")]
            public string? Rate { get; set; }
        }
    }
}
