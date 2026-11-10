using System.Net;
using System.Text.Json;
using Amazon.ElastiCache;
using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.Aws.Helpers.Options;
using Common.Aws.Helpers.Test;
using Common.Lambda.Helpers.Utilities;
using Lambda.DeleteOldConnections.Exceptions;
using Lambda.DeleteOldConnections.Structures;
using Shared.Lambda.Structures;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Lambda.DeleteOldConnections
{
    public partial class Function
    {
        private AmazonSecretsManagerClient _awsSecretsManager = null!;
        private AmazonElastiCacheClient _awsElastiCache = null!;

        public async Task<Dictionary<string, object>> FunctionHandler(string input, ILambdaContext context)
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
                    eventJson.OldUsersSecretId
                ]
            });

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
            Utility.TryDeserializeJson<OldUsers>(
                json: secrets.SecretValues
                    .Where(s => s.Name == eventJson.OldUsersSecretId)
                    .Single()
                    .SecretString,
                section: null,
                out var oldUsers);
            
            var caBundle = TlsUtility.LoadCertificateChain(cABundleOptions!.Value);

            // Delete old connections options
            var now = DateTimeOffset.UtcNow;
            var deletionResults = new DeletionResults();

            context.Logger.LogInformation($"The deletion of the old connections options has been started. Date: {now.ToString("yyyy-MM")}");

            try
            {
                await DeletePostgreSqlUsersAsync(
                    adminConnections!.PostgreSql,
                    caBundle,
                    oldUsers!.PostgreSqlUsers,
                    deletionResults,
                    context.Logger);
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"PostgreSQL - Something went wrong. Message: {exception.Message}.");
            }

            try
            {
                await DeleteRabbitMqUsersAsync(
                    adminConnections!.RabbitMq,
                    caBundle,
                    eventJson.Environment,
                    oldUsers!.RabbitMqUsers,
                    deletionResults,
                    context.Logger);
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"RabbitMQ - Something went wrong. Message: {exception.Message}.");
            }

            try
            {
                await DeleteRedisUsersAsync(
                    oldUsers!.RedisUsers,
                    deletionResults,
                    context.Logger);
            }
            catch (Exception exception)
            {
                context.Logger.LogError(exception, $"Redis - Something went wrong. Message: {exception.Message}.");
            }

            // If the number of the old connections options does not match with the number of
            // users in the results, it means some users could not deleted, and they should be
            // returned in the lambda function.
            CheckProcessedUsers(
                allUsers: oldUsers!.PostgreSqlUsers.Select(u => $"{u.Database}/{u.Username}").ToList(),
                deletedUsers: deletionResults.DeletedPostgreSqlUsers,
                failedUsers: deletionResults.FailedPostgreSqlUsers);
            CheckProcessedUsers(
                allUsers: oldUsers.RabbitMqUsers,
                deletedUsers: deletionResults.DeletedRabbitMqUsers,
                failedUsers: deletionResults.FailedRabbitMqUsers);
            CheckProcessedUsers(
                allUsers: oldUsers.RedisUsers.Select(u => $"{u.UserGroupId}/{u.Username}").ToList(),
                deletedUsers: deletionResults.DeletedRedisUsers,
                failedUsers: deletionResults.FailedRedisUsers);

            return new Dictionary<string, object>()
            {
                { "statusCode", GetHttpStatusCodeFromResults(oldUsers, deletionResults) },
                { "users", JsonSerializer.Serialize(deletionResults) }
            };
        }

        private void CheckProcessedUsers(
            List<string> allUsers,
            List<string> deletedUsers,
            List<string> failedUsers)
        {
            var processedUsers = deletedUsers.Concat(failedUsers).ToHashSet();
            if (processedUsers.Count == allUsers.Count)
                return;

            foreach (var user in allUsers)
            {
                if (!processedUsers.Contains(user))
                {
                    failedUsers.Add(user);
                }
            }
        }

        private HttpStatusCode GetHttpStatusCodeFromResults(OldUsers oldUsers, DeletionResults deletionResults)
        {
            var totalUsers = oldUsers.PostgreSqlUsers.Count +
                oldUsers.RabbitMqUsers.Count +
                oldUsers.RedisUsers.Count;
            var totalFailures = deletionResults.FailedPostgreSqlUsers.Count +
                deletionResults.FailedRabbitMqUsers.Count +
                deletionResults.FailedRedisUsers.Count;

            if (totalFailures != 0)
            {
                return totalFailures == totalUsers ?
                    HttpStatusCode.InternalServerError :
                    HttpStatusCode.MultiStatus;
            }

            return HttpStatusCode.OK;
        }
    }
}
