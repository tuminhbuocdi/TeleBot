
using Management.Api.Extensions;
using Management.Application.Auth;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using Management.Api.Extensions;

namespace Management.Backend
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddCors(opt =>
            {
                opt.AddPolicy("frontend", p =>
                {
                    p.WithOrigins("http://localhost:5173", "https://localhost:5173")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            builder.Services.AddControllers();
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen();
            builder.Services.AddHttpClient();
            builder.Services.AddInfrastructure();

            var jwtKey = builder.Configuration["Jwt:Key"];
            if (string.IsNullOrWhiteSpace(jwtKey) || Encoding.UTF8.GetBytes(jwtKey).Length < 32)
            {
                throw new InvalidOperationException("Invalid configuration: Jwt:Key must be at least 32 bytes (256 bits) for HS256.");
            }

            builder.Services.AddAuthentication("Bearer")
                .AddJwtBearer("Bearer", opt =>
                {
                    opt.TokenValidationParameters = new()
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,

                        ClockSkew = TimeSpan.Zero,

                        ValidIssuer = builder.Configuration["Jwt:Issuer"],
                        ValidAudience = builder.Configuration["Jwt:Audience"],

                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtKey))
                    };
                });

            builder.Services.AddAuthorization();

            builder.Services.AddSignalR();

            var app = builder.Build();

            app.UseSwagger();
            app.UseSwaggerUI();

            app.UseCors("frontend");

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();
            app.MapHub<Management.Api.Hubs.AppHub>("/hub");

            app.Run();

        }
    }
}
