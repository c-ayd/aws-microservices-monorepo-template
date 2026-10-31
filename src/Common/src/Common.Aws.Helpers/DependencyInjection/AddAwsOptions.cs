using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Common.Aws.Helpers.Test;

namespace Common.Aws.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddAwsOptions(this IServiceCollection services, bool isProduction)
        {
            AwsConfig.IsProduction = isProduction;

            services.AddDefaultAWSOptions(sp =>
            {
                var awsOptions = sp.GetRequiredService<IConfiguration>().GetAWSOptions();
                if (!AwsConfig.IsProduction)
                {
                    awsOptions.Credentials = AwsTestConstants.Credentials;
                }

                return awsOptions;
            });
        }
    }
}
