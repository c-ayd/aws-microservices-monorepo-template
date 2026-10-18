using Amazon.Runtime;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Shared.Aws.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddAwsOptions(this IServiceCollection services, bool isProduction)
        {
            services.AddDefaultAWSOptions(sp =>
            {
                var awsOptions = sp.GetRequiredService<IConfiguration>().GetAWSOptions();
                if (!isProduction)
                {
                    awsOptions.Credentials = new BasicAWSCredentials("test", "test");
                }

                return awsOptions;
            });
        }
    }
}
