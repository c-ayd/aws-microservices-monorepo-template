using System.Net;
using System.Text.Json;
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
        private AmazonSecretsManagerClient _awsSecretsManager = null!;
        private AmazonElastiCacheClient _awsElastiCache = null!;
        private AmazonECSClient _awsEcs = null!;

        private const int _batchSize = 100;

        public async Task<Dictionary<string, object>> FunctionHandler(string input, ILambdaContext context)
        {
            // Get event JSON from input
            if (!Common.Lambda.Helpers.Utilities.Utility.TryDeserializeJson<EventJson>(input, section: "detail", out var eventJson))
                throw new EvenJsonFormatException();
            
            // Set AWS clients
            if (eventJson!.Environment == null || eventJson.Environment.IsProduction)
            {
                _awsSecretsManager = new AmazonSecretsManagerClient();
                _awsElastiCache = new AmazonElastiCacheClient();
                _awsEcs = new AmazonECSClient();
            }
            else
            {
                _awsSecretsManager = new AmazonSecretsManagerClient(AwsTestConstants.Credentials, new AmazonSecretsManagerConfig()
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
            var secrets = await _awsSecretsManager.BatchGetSecretValueAsync(new BatchGetSecretValueRequest()
            {
                SecretIdList = [
                    eventJson.JwtKeysSecretId,
                    eventJson.Certificate.IntermediateCASecretId,
                    eventJson.Certificate.CABundleSecretId,
                    eventJson.Connection.AdminConnectionsSecretId,
                    ..eventJson.Services
                        .Where(s => s.Certificate != null)
                        .Select(s => s.Certificate!.CertificateSecretId),
                    ..eventJson.Services
                        .Where(s => s.Connection != null)
                        .Select(s => s.Connection!.ConncetionsSecretId)
                ]
            });

            // Update secret values
            var now = DateTimeOffset.UtcNow;
            var startDate = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
            var jwtKeysRotationResult = false;
            var servicesResults = new Dictionary<string, ServiceRotationResult>();
            foreach (var service in eventJson.Services)
            {
                servicesResults.Add(service.ServiceName, new ServiceRotationResult());
            }

            context.Logger.LogInformation($"The rotation has been started. Date: {now.ToString("yyyy-MM")}");

            try
            {
                await UpdateJwtKeysAsync(
                    secrets,
                    eventJson.JwtKeysSecretId,
                    startDate,
                    context.Logger);
                
                jwtKeysRotationResult = true;
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"JWT - Something went wrong. Message: {exception.Message}.");
            }

            try
            {
                await UpdateCertificatesAsync(
                    secrets,
                    eventJson.Certificate,
                    eventJson.Services,
                    startDate,
                    servicesResults,
                    context.Logger);
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"Certificates - Something went wrong. Message: {exception.Message}.");
            }

            try
            {
                await UpdateConnectionsAsync(
                    secrets,
                    eventJson.Certificate,
                    eventJson.Connection,
                    eventJson.Services,
                    eventJson.Environment,
                    startDate,
                    servicesResults,
                    context.Logger);
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"Connections - Something went wrong. Message: {exception.Message}.");
            }
            
            // Restart AWS ECS tasks
            await RestartEcsTasks(eventJson.EcsClusterName, servicesResults, context.Logger);

            return new Dictionary<string, object>()
            {
                { "statusCode", GetHttpStatusCodeFromResults(jwtKeysRotationResult, servicesResults) },
                { "jwtKeys", jwtKeysRotationResult },
                { "services", JsonSerializer.Serialize(servicesResults) }
            };
        }

        private HttpStatusCode GetHttpStatusCodeFromResults(
            bool jwtKeysRotationResult,
            Dictionary<string, ServiceRotationResult> serviceResults)
        {
            var failedServicesCount = serviceResults
                .Where(r => r.Value.AnyFailure())
                .Count();

            if (!jwtKeysRotationResult && failedServicesCount == serviceResults.Count)
                return HttpStatusCode.InternalServerError;

            if (!jwtKeysRotationResult || failedServicesCount != 0)
                return HttpStatusCode.MultiStatus;
            
            return HttpStatusCode.OK;
        }
    }
}
