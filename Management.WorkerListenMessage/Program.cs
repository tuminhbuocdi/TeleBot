namespace Management.WorkerListenMessage
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSingleton<Management.Infrastructure.Db.DbConnectionFactory>();
            builder.Services.AddSingleton<Management.Infrastructure.Repositories.TelegramPostRepository>();
            builder.Services.AddSingleton<Management.Infrastructure.Repositories.TelegramPostAdminRepository>();

            builder.Services.Configure<TelegramUploaderOptions>(builder.Configuration.GetSection("TelegramUploader"));

            // Register post resend services
            builder.Services.AddSingleton<Services.ITelegramPostResendService, Services.TelegramPostResendService>();
            builder.Services.AddHostedService<Services.TelegramStartBotWorker>();
            builder.Services.AddHostedService<Services.TelegramPostResendWorker>();

            var host = builder.Build();
            host.Run();
        }
    }
}