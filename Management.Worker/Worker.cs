namespace Management.Worker
{
    public class Worker : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                Console.WriteLine("Bot running...");
                await Task.Delay(5000, stoppingToken);
            }
        }
    }

}
