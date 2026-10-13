using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Http.Request
{
    public static partial class RequestHelper
    {
        public static void ConfigureForwardedHeaders(this IServiceCollection services, params string[] cidrBlocks)
        {
            services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor;

                options.KnownIPNetworks.Clear();
                options.KnownProxies.Clear();

                foreach (var cidrBlock in cidrBlocks)
                {
                    options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(cidrBlock));
                }
            });
        }
    }
}
