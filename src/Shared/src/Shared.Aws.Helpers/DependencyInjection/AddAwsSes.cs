using Amazon.SimpleEmailV2;
using Microsoft.Extensions.DependencyInjection;
using Shared.Aws.Helpers.Abstractions;

namespace Shared.Aws.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddAwsSes(this IServiceCollection services)
        {
            services.AddAWSService<IAmazonSimpleEmailServiceV2>();
            services.AddSingleton<IAwsSes, AwsSes>();
        }
    }
}
