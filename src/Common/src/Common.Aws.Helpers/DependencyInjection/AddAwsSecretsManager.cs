using Amazon.SecretsManager;
using Microsoft.Extensions.DependencyInjection;
using Common.Aws.Helpers.Abstractions;

namespace Common.Aws.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddAwsSecretsManager(this IServiceCollection services)
        {
            services.AddAWSService<IAmazonSecretsManager>();
            services.AddSingleton<IAwsSecretsManager, AwsSecretsManager>();
        }
    }
}
