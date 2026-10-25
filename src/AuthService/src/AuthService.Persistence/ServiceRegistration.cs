using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Abstractions.DistributedCaches;
using AuthService.Persistence.DbContexts;
using AuthService.Persistence.DistributedCaches;
using AuthService.Application.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.PostgreSql.Helpers.EntityFramework.Extensions;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;

namespace AuthService.Persistence
{
    public static class ServiceRegistration
    {
        public static void AddPersistenceServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddDbContext<AuthDbContext>(builder =>
            {
                builder.ConfigureDbContext(configuration.GetSection(ConnectionsOptions.Key).Get<ConnectionsOptions>()!.AuthDb,
                    configuration.GetSection(CABundleOptions.Key).Get<CABundleOptions>()!.Value);
            });
            services.AddScoped<IAuthDbContext>(sp => sp.GetRequiredService<AuthDbContext>());

            services.AddDbContext<DroppedMessagesDbContext>(builder =>
            {
                builder.ConfigureDbContext(configuration.GetSection(ConnectionsOptions.Key).Get<ConnectionsOptions>()!.DroppedMessagesDb,
                    configuration.GetSection(CABundleOptions.Key).Get<CABundleOptions>()!.Value);
            });

            services.AddSingleton<ITokenBlacklistRedis, TokenBlacklistRedis>();
        }
    }
}
