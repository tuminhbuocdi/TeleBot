namespace Management.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSingleton<Management.Infrastructure.Db.DbConnectionFactory>();
            builder.Services.AddSingleton<Management.Infrastructure.Repositories.TelegramPostRepository>();

            builder.Services.AddSingleton<Management.Infrastructure.Repositories.TelegramCrawlRepository>();

            builder.Services.Configure<TelegramUploaderOptions>(builder.Configuration.GetSection("TelegramUploader"));
            builder.Services.Configure<TelegramMtProtoOptions>(builder.Configuration.GetSection("TelegramMtProto"));
            builder.Services.Configure<TelegramCrawlJobOptions>(builder.Configuration.GetSection("TelegramCrawlJob"));
            builder.Services.Configure<TelegramVideoDemoOptions>(builder.Configuration.GetSection("TelegramVideoDemo"));

            builder.Services.AddSingleton<Services.TelegramPublicChannelUploader>();
            builder.Services.AddSingleton<Services.TelegramMtProtoClientProvider>();
            builder.Services.AddHostedService<Jobs.TelegramMtProtoCrawlJobWorker>();

            var host = builder.Build();
            host.Run();
        }
    }
}