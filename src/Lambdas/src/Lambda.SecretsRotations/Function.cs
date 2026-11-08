using System.Net;
using Amazon.ECS;
using Amazon.ElastiCache;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Common.Aws.Helpers.Test;
using Lambda.SecretsRotations.Exceptions;
using Lambda.SecretsRotations.Structures;
using Lambda.SecretsRotations.Utilities;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Lambda.SecretsRotations
{
    public partial class Function
    {
        public static AmazonSecretsManagerClient AwsSecretsManager { get; private set; } = null!;
        private static AmazonElastiCacheClient _awsElastiCache = null!;
        private static AmazonECSClient _awsEcs = null!;

        public async Task<Dictionary<string, object>> FunctionHandler(string input, ILambdaContext context)
        {
            // Get event JSON from input
            var eventJson = Utility.GetOptionsFromJson<EventJson>(input, elementName: "detail");
            if (eventJson == null)
                throw new EvenJsonFormatException();

            // Set AWS clients
            if (eventJson.Environment == null || eventJson.Environment.IsProduction)
            {
                AwsSecretsManager = new AmazonSecretsManagerClient();
                _awsElastiCache = new AmazonElastiCacheClient();
                _awsEcs = new AmazonECSClient();
            }
            else
            {
                AwsSecretsManager = new AmazonSecretsManagerClient(AwsTestConstants.Credentials, new AmazonSecretsManagerConfig()
                {
                    ServiceURL = eventJson.Environment.ServiceUrl,
                    AuthenticationRegion = AwsTestConstants.Region
                });
                _awsElastiCache = new AmazonElastiCacheClient(AwsTestConstants.Credentials, new AmazonElastiCacheConfig()
                {
                    ServiceURL = eventJson.Environment.ServiceUrl,
                    AuthenticationRegion = AwsTestConstants.Region
                });
                _awsEcs = new AmazonECSClient(AwsTestConstants.Credentials, new AmazonECSConfig()
                {
                    ServiceURL = eventJson.Environment.ServiceUrl,
                    AuthenticationRegion = AwsTestConstants.Region
                });
            }

            // Get required secrets all at once
            var secrets = await AwsSecretsManager.BatchGetSecretValueAsync(new BatchGetSecretValueRequest()
            {
                SecretIdList = [
                    eventJson.JwtKeysSecretId,
                    eventJson.Certificates.IntermediateCASecretId,
                    ..eventJson.Certificates.Services.Select(s => s.CertificateSecretId),
                    eventJson.Connections.CABundleSecretId,
                    eventJson.Connections.AdminConnectionsSecretId,
                    ..eventJson.Connections.Services.Select(s => s.ConncetionsSecretId)
                ]
            });

            // Update secrets
            var now = DateTimeOffset.UtcNow;
            var startDate = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var results = new Dictionary<string, ServiceRotationResult>();

            context.Logger.LogInformation($"The rotation has been started. Date: {now.ToString("yyyy-MM")}");

            await UpdateJwtKeys(secrets, eventJson.JwtKeysSecretId, startDate, context.Logger);
            await UpdateCertificates(secrets, eventJson.Certificates, startDate, results, context.Logger);
            await UpdateConnectionCredentials(secrets, eventJson.Connections, eventJson.Environment, startDate, results, context.Logger);

            // Restart AWS ECS tasks
            await RestartEcsTasks(eventJson.EcsClusterName, results, context.Logger);

            return new Dictionary<string, object>()
            {
                { "statusCode", HttpStatusCode.OK },
                { "failedServices", results
                    .Where(p => p.Value.IsFailed())
                    .Select(p => new
                    {
                        Name = p.Key,
                        Results = p.Value
                    })
                    .ToList() }
            };
        }
    }
}
