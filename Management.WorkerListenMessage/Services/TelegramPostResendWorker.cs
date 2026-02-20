using Microsoft.Extensions.Hosting;

namespace Management.WorkerListenMessage.Services;

public sealed class TelegramPostResendWorker : BackgroundService
{
    private readonly ILogger<TelegramPostResendWorker> _logger;
    private readonly ITelegramPostResendService _resendService;

    public TelegramPostResendWorker(
        ILogger<TelegramPostResendWorker> logger,
        ITelegramPostResendService resendService)
    {
        _logger = logger;
        _resendService = resendService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TelegramPostResendWorker starting - will resend oldest post every 2 hours");

        // Initial delay of 1 hour before first resend
        await Task.Delay(TimeSpan.FromHours(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                _logger.LogInformation("Starting scheduled post resend at: {time}", DateTimeOffset.Now);
                
                await _resendService.ResendOldestPostAsync(stoppingToken);
                
                _logger.LogInformation("Post resend completed, next run in 2 hours");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled post resend");
            }

            // Wait 2 hours before next run
            await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
        }
    }
}
