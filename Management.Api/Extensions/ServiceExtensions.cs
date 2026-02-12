using Management.Application.Auth;
using Management.Infrastructure.Db;
using Management.Infrastructure.Repositories;

namespace Management.Api.Extensions;

public static class ServiceExtension
{
    public static void AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<DbConnectionFactory>();
        services.AddScoped<UnitOfWork>();
        services.AddScoped<JwtService>();


        services.AddScoped<UserRepository>();
    }
}
