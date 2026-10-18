using Amazon.KeyManagementService;
using Microsoft.Extensions.DependencyInjection;
using Shared.Aws.Helpers.Abstractions;

namespace Shared.Aws.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddAwsKms(this IServiceCollection services)
        {
            services.AddAWSService<IAmazonKeyManagementService>();
            services.AddSingleton<IKeyManagement, AwsKms>();
        }
    }
}
