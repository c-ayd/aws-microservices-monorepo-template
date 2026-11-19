using System.Net;
using System.Text.Json;
using Amazon.ElastiCache;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.Aws.Helpers.Options;
using Common.Aws.Helpers.Test;
using Common.Lambda.Helpers.Exceptions;
using Common.Lambda.Helpers.Utilities;
using Lambda.DeleteOldConnections.Structures;
using Shared.Lambda.Structures;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Lambda.DeleteOldConnections
{
    public partial class Function
    {
        private AmazonSecretsManagerClient _awsSecretsManager = null!;
        private AmazonElastiCacheClient _awsElastiCache = null!;

        public async Task<Dictionary<string, object?>> FunctionHandler(string input, ILambdaContext context)
        {
            // Get event JSON from input
            if (!Utility.TryDeserializeJson<EventJson>(input, section: "detail", out var eventJson))
                throw new EvenJsonFormatException();

            // Set AWS clients
            if (eventJson!.Environment == null || eventJson.Environment.IsProduction)
            {
                _awsSecretsManager = new AmazonSecretsManagerClient();
                _awsElastiCache = new AmazonElastiCacheClient();
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
            }

            // Get required secrets all at once
            var secrets = await _awsSecretsManager.BatchGetSecretValueAsync(new BatchGetSecretValueRequest()
            {
                SecretIdList = [
                    eventJson.CABundleSecretId,
                    eventJson.AdminConnectionsSecretId,
                    eventJson.OldCredentialsSecretId
                ]
            });

            Utility.TryDeserializeJson<OldCredentials>(
                json: secrets.SecretValues
                    .Where(s => s.Name == eventJson.OldCredentialsSecretId)
                    .Single()
                    .SecretString,
                section: null,
                out var oldCredentials);
            
            if (oldCredentials!.IsEmpty())
            {
                context.Logger.LogInformation($"There is no old credentials to delete. Date: {DateTimeOffset.UtcNow.ToString("yyyy-MM")}");

                return new Dictionary<string, object?>()
                {
                    { "statusCode", HttpStatusCode.InternalServerError },
                    { "credentials", null }
                };
            }

            Utility.TryDeserializeJson<CABundleOptionsBase>(
                json: secrets.SecretValues
                    .Where(s => s.Name == eventJson.CABundleSecretId)
                    .Single()
                    .SecretString,
                section: CABundleOptionsBase.Key,
                out var cABundleOptions);
            Utility.TryDeserializeJson<AdminConnections>(
                json: secrets.SecretValues
                    .Where(s => s.Name == eventJson.AdminConnectionsSecretId)
                    .Single()
                    .SecretString,
                section: null,
                out var adminConnections);
            
            var caBundle = TlsUtility.LoadCertificateChain(cABundleOptions!.Value);

            // Delete old connections options
            var deletionResults = new DeletionResults();

            context.Logger.LogInformation($"The deletion of the old credentials has been started. Date: {DateTimeOffset.UtcNow.ToString("yyyy-MM")}");

            try
            {
                await DeletePostgreSqlCredentialsAsync(
                    adminConnections!.PostgreSql,
                    caBundle,
                    oldCredentials!.PostgreSql,
                    deletionResults,
                    context.Logger);
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"PostgreSQL - Something went wrong. Message: {exception.Message}.");
            }

            try
            {
                await DeleteRabbitMqCredentialsAsync(
                    adminConnections!.RabbitMq,
                    caBundle,
                    eventJson.Environment,
                    oldCredentials!.RabbitMq,
                    deletionResults,
                    context.Logger);
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"RabbitMQ - Something went wrong. Message: {exception.Message}.");
            }

            try
            {
                await DeleteRedisCredentialsAsync(
                    oldCredentials!.Redis,
                    deletionResults,
                    context.Logger);
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"Redis - Something went wrong. Message: {exception.Message}.");
            }

            // If the number of the old connections options does not match with the number of
            // credentials in the results, it means some credentials could not deleted, and
            // they should be returned in the lambda function.
            CheckProcessedCredentials(
                allCredentials: oldCredentials!.PostgreSql.Select(u => $"{u.Database}/{u.Username}").ToList(),
                deletedCredentials: deletionResults.DeletedPostgreSqlCredentials,
                failedCredentials: deletionResults.FailedPostgreSqlCredentials);
            CheckProcessedCredentials(
                allCredentials: oldCredentials.RabbitMq,
                deletedCredentials: deletionResults.DeletedRabbitMqCredentials,
                failedCredentials: deletionResults.FailedRabbitMqCredentials);
            CheckProcessedCredentials(
                allCredentials: oldCredentials.Redis.Select(u => $"{u.UserGroupId}/{u.Username}").ToList(),
                deletedCredentials: deletionResults.DeletedRedisCredentials,
                failedCredentials: deletionResults.FailedRedisCredentials);

            return new Dictionary<string, object?>()
            {
                { "statusCode", GetHttpStatusCodeFromResults(oldCredentials, deletionResults) },
                { "credentials", JsonSerializer.Serialize(deletionResults) }
            };
        }

        private void CheckProcessedCredentials(
            List<string> allCredentials,
            List<string> deletedCredentials,
            List<string> failedCredentials)
        {
            var processedCredentials = deletedCredentials.Concat(failedCredentials).ToHashSet();
            if (processedCredentials.Count == allCredentials.Count)
                return;

            foreach (var credentials in allCredentials)
            {
                if (!processedCredentials.Contains(credentials))
                {
                    failedCredentials.Add(credentials);
                }
            }
        }

        private HttpStatusCode GetHttpStatusCodeFromResults(OldCredentials oldCredentials, DeletionResults deletionResults)
        {
            var totalCredentials = oldCredentials.PostgreSql.Count +
                oldCredentials.RabbitMq.Count +
                oldCredentials.Redis.Count;
            var totalFailures = deletionResults.FailedPostgreSqlCredentials.Count +
                deletionResults.FailedRabbitMqCredentials.Count +
                deletionResults.FailedRedisCredentials.Count;

            if (totalFailures != 0)
            {
                return totalFailures == totalCredentials ?
                    HttpStatusCode.InternalServerError :
                    HttpStatusCode.MultiStatus;
            }

            return HttpStatusCode.OK;
        }
    }
}
