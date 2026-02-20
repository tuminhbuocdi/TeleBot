using Management.Application.Auth;
using Management.Infrastructure.Db;
using Management.Infrastructure.Repositories;
using Management.Api.Services;

namespace Management.Api.Extensions;

public static class ServiceExtension
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<DbConnectionFactory>();
        services.AddScoped<UnitOfWork>();
        services.AddScoped<JwtService>();
        services.AddSingleton<PasswordHasher>();


        services.AddScoped<UserRepository>();
        services.AddScoped<CrashRecordRepository>();
        services.AddScoped<TelegramPostAdminRepository>();
        services.AddScoped<TelegramCrawlRepository>();

        services.AddSingleton<TelegramNotificationService>();
    }
}
