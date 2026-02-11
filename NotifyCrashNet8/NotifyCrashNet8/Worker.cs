using Microsoft.Extensions.Hosting;

namespace NotifyCrashNet8;

public sealed class Worker : BackgroundService
{
    private readonly CrashClient _crashClient;

    public Worker(CrashClient crashClient)
    {
        _crashClient = crashClient;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _crashClient.Start(stoppingToken);
    }
}
