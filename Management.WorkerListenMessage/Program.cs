namespace Management.WorkerListenMessage
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSingleton<Management.Infrastructure.Db.DbConnectionFactory>();
            builder.Services.AddSingleton<Management.Infrastructure.Repositories.TelegramPostRepository>();

            builder.Services.Configure<TelegramUploaderOptions>(builder.Configuration.GetSection("TelegramUploader"));

            builder.Services.AddHostedService<Services.TelegramStartBotWorker>();

            var host = builder.Build();
            host.Run();
        }
    }
}