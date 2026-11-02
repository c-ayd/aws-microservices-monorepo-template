using Microsoft.Extensions.DependencyInjection;

namespace Common.Grpc.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        /// <summary>
        /// Registers gRPC for gRPC servers.
        /// </summary>
        /// <param name="isProduction">Whether the running environment is production</param>
        public static void AddGrpcServer(this IServiceCollection services, bool isProduction)
        {
            services.AddGrpc(options =>
            {
                options.EnableDetailedErrors = !isProduction;
            });
        }
    }
}
