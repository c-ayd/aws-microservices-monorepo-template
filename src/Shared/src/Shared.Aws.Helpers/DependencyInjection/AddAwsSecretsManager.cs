using Amazon.SecretsManager;
using Microsoft.Extensions.DependencyInjection;
using Shared.Aws.Helpers.Abstractions;

namespace Shared.Aws.Helpers.DependencyInjection
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
