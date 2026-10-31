using Microsoft.Extensions.DependencyInjection;

namespace Shared.Grpc.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddGrpcServer(this IServiceCollection services, bool isProduction)
        {
            services.AddGrpc(options =>
            {
                options.EnableDetailedErrors = !isProduction;
            });
        }
    }
}
