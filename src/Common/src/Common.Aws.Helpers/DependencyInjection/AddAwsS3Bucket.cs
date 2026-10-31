using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Common.Aws.Helpers.Abstractions;

namespace Common.Aws.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddAwsS3Bucket(this IServiceCollection services)
        {
            services.AddAWSService<IAmazonS3>((config, sp) =>
            {
                if (!AwsConfig.IsProduction)
                {
                    ((AmazonS3Config)config).ForcePathStyle = true;
                }
            });
            services.AddSingleton<IAwsS3Bucket, AwsS3Bucket>();
        }
    }
}
