using Amazon.KeyManagementService;
using Microsoft.Extensions.DependencyInjection;
using Common.Aws.Helpers.Abstractions;

namespace Common.Aws.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddAwsKms(this IServiceCollection services)
        {
            services.AddAWSService<IAmazonKeyManagementService>();
            services.AddSingleton<IAwsKms, AwsKms>();
        }
    }
}
