using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;
using System.Text.Json;
using Amazon.ElastiCache.Model;
using Amazon.Lambda.Core;
using Amazon.SecretsManager.Model;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.Aws.Helpers.Options;
using Common.PostgreSql.Helpers.Options.Extensions;
using Common.RabbitMq.Helpers.Options.Extensions;
using Common.Redis.Helpers.Options.Extensions;
using Lambda.SecretsRotations.Structures;
using Lambda.SecretsRotations.Utilities;
using Microsoft.Net.Http.Headers;
using Npgsql;

namespace Lambda.SecretsRotations
{
    public partial class Function
    {
        private async Task UpdateConnectionsAsync(
            BatchGetSecretValueResponse secrets,
            EventJson.CertificateInfo certificateInfo,
            EventJson.ConnectionInfo connectionInfo,
            List<EventJson.ServiceInfo> services,
            EventJson.EnvironmentInfo? environmentInfo,
            DateTimeOffset startDate,
            Dictionary<string, ServiceRotationResult> serviceResults,
            ILambdaLogger logger)
        {
            var startDatePostfix = startDate.ToString("yyyy-MM");
            var oldConnectionsToDelete = new OldConnections();

            // Deserialize secrets required for connecting infrastructure services and TLS handshake
            var adminConnections = JsonSerializer.Deserialize<AdminConnections>(secrets.SecretValues
                .Where(s => s.Name == connectionInfo.AdminConnectionsSecretId)
                .Single()
                .SecretString)!;
            Utility.TryDeserializeJson<CABundleOptionsBase>(
                json: secrets.SecretValues
                .Where(s => s.Name == certificateInfo.CABundleSecretId)
                .Single()
                .SecretString,
                section: CABundleOptionsBase.Key,
                out var caBundleOptions);
            var caBundle = TlsUtility.LoadCertificateChain(caBundleOptions!.Value);

            // Establish connection to PostgreSQL
            var postgreSqlConnStringBuilder = new NpgsqlConnectionStringBuilder()
            {
                Host = adminConnections.PostgreSql.Host,
                Port = adminConnections.PostgreSql.Port,
                Database = adminConnections.PostgreSql.Database,
                Username = adminConnections.PostgreSql.Username,
                Password = adminConnections.PostgreSql.Password,
                SslMode = SslMode.Require
            };

            var postgreSqlSourceBuilder = new NpgsqlDataSourceBuilder(postgreSqlConnStringBuilder.ConnectionString);
            postgreSqlSourceBuilder.UseSslClientAuthenticationOptionsCallback(options =>
            {
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                options.TargetHost = adminConnections.PostgreSql.Ssl.ServerName;
                options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                };
            });

            await using var postgreSqlDataSource = postgreSqlSourceBuilder.Build();
            await using var postgreSqlConnection = await postgreSqlDataSource.OpenConnectionAsync();

            // Create HTTP client for RabbitMQ
            using var rabbitMqHttpClient = new HttpClient(new SocketsHttpHandler()
            {
                SslOptions = new SslClientAuthenticationOptions()
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if ((environmentInfo != null && !environmentInfo.IsProduction) &&
                            ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0))
                        {
                            sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
                        }

                        return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                    }
                },
            });
            rabbitMqHttpClient.BaseAddress = new Uri($"https://{adminConnections.RabbitMq.Host}:{adminConnections.RabbitMq.Port}");
            rabbitMqHttpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Basic " +
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{adminConnections.RabbitMq.Username}:{adminConnections.RabbitMq.Password}")));

            // Update connection secret values
            var updateTasks = new Dictionary<string, Task>();
            foreach (var service in services)
            {
                if (service.Connection == null)
                    continue;

                using var jsonDoc = JsonDocument.Parse(secrets.SecretValues
                    .Where(s => s.Name == service.Connection.ConncetionsSecretId)
                    .Single()
                    .SecretString);

                var newConnectionsOptions = new Dictionary<string, object>();
                var oldConnectionsOptions = jsonDoc.RootElement.GetProperty(ConnectionsOptionsBase.Key);
                foreach (var connectionJsonProperty in oldConnectionsOptions.EnumerateObject())
                {
                    var connectionJson = connectionJsonProperty.Value.GetRawText();
                    if (Utility.TryDeserializeJson<PostgreSqlConnectionOptions>(
                        connectionJson,
                        section: null,
                        out var postgreSqlConnectionOptions))
                    {
                        serviceResults[service.ServiceName].Connections.PostgreSql = false;

                        try
                        {
                            await UpdateConnections_CreateNewPostgresqlRoleAsync(
                                postgreSqlConnection,
                                startDatePostfix,
                                postgreSqlConnectionOptions!,
                                oldConnectionsToDelete,
                                logger);

                            newConnectionsOptions.Add(connectionJsonProperty.Name, postgreSqlConnectionOptions!);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, $"Connection_PostgreSQL ({postgreSqlConnectionOptions!.Username}) - Something went wrong. Message {exception.Message}.");
                        }
                    }
                    else if (Utility.TryDeserializeJson<RabbitMqConnectionOptions>(
                        connectionJson,
                        section: null,
                        out var rabbitMqConnectionOptions))
                    {
                        serviceResults[service.ServiceName].Connections.RabbitMq = false;

                        try
                        {
                            await UpdateConnections_CreateNewRabbitMqUserAsync(
                                rabbitMqHttpClient,
                                startDatePostfix,
                                rabbitMqConnectionOptions!,
                                oldConnectionsToDelete,
                                logger);

                            newConnectionsOptions.Add(connectionJsonProperty.Name, rabbitMqConnectionOptions!);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, $"Connection_RabbitMQ ({rabbitMqConnectionOptions!.Username}) - Something went wrong. Message {exception.Message}.");
                        }
                    }
                    else if (Utility.TryDeserializeJson<RedisConnectionOptions>(
                        connectionJson,
                        section: null,
                        out var redisConnectionOptions))
                    {
                        serviceResults[service.ServiceName].Connections.Redis = false;

                        try
                        {
                            await UpdateConnections_CreateNewRedisUserAsync(
                                startDatePostfix,
                                redisConnectionOptions!,
                                oldConnectionsToDelete,
                                logger);

                            newConnectionsOptions.Add(connectionJsonProperty.Name, redisConnectionOptions!);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, $"Connection_Redis ({redisConnectionOptions!.Username}) - Something went wrong. Message {exception.Message}.");
                        }
                    }
                    else
                    {
                        logger.LogCritical($"Unknown connection type. Service Name: {service.ServiceName}, JSON Key: {connectionJsonProperty.Name}.");
                    }
                }

                // Add task to the list for batch update
                updateTasks.Add(service.ServiceName, AwsSecretsManager.PutSecretValueAsync(new PutSecretValueRequest()
                {
                    SecretId = service.Connection.ConncetionsSecretId,
                    SecretString = JsonSerializer.Serialize(new Dictionary<string, object>()
                    {
                        { ConnectionsOptionsBase.Key, newConnectionsOptions }
                    })
                }));

                // Wait for the batch to be completed before creating more credentials
                if (updateTasks.Count >= _batchSize)
                {
                    await UpdateConnectionCredentials_WaitForUpdateTasksAsync(updateTasks, serviceResults, logger);

                    // Wait 1 second to avoid API throttling
                    await Task.Delay(1000);
                }
            }

            // Wait for the last batch if there is any
            if (updateTasks.Count != 0)
            {
                await UpdateConnectionCredentials_WaitForUpdateTasksAsync(updateTasks, serviceResults, logger);
            }

            // Save the old connection options to AWS Secrets Manager to delete them later in another lambda fuction
            await AwsSecretsManager.PutSecretValueAsync(new PutSecretValueRequest()
            {
                SecretId = connectionInfo.OldConnectionsSecretId,
                SecretString = JsonSerializer.Serialize(oldConnectionsToDelete)
            });

            logger.LogInformation("Connection_FINAL - The old connection credentials are saved to AWS Secrets Manager.");
        }

        private async Task UpdateConnections_CreateNewPostgresqlRoleAsync(
            NpgsqlConnection connection,
            string startDatePostfix,
            PostgreSqlConnectionOptions oldConnectionOptions,
            OldConnections oldConnectionsToDelete,
            ILambdaLogger logger)
        {
            var baseUsername = oldConnectionOptions.Username.Split('_')[0];
            var newUsername = $"{baseUsername}_{startDatePostfix}";
            var newPassword = Utility.GeneratePassword(length: 50);

            // Create a new user and copy the permissions from the template user
            await using var transaction = await connection.BeginTransactionAsync();

            await using (var command = new NpgsqlCommand(
                $@"CREATE ROLE ""{newUsername}"" LOGIN PASSWORD '{newPassword}';
                GRANT ""{baseUsername}_default"" TO ""{newUsername}"";",
                connection,
                transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            logger.LogInformation($"Connection_PostgreSQL ({oldConnectionOptions.Username}) - A new user has been created.");

            // Add the old role to the deletion list
            oldConnectionsToDelete.PostgreSqlRoles.Add(oldConnectionOptions.Username);

            // Update old values
            oldConnectionOptions.Username = newUsername;
            oldConnectionOptions.Password = newPassword;
        }

        private async Task UpdateConnections_CreateNewRabbitMqUserAsync(
            HttpClient rabbitMqHttpClient,
            string startDatePostfix,
            RabbitMqConnectionOptions oldConnectionOptions,
            OldConnections oldConnectionsToDelete,
            ILambdaLogger logger)
        {
            var newUsername = $"{oldConnectionOptions.Username.Split('_')[0]}_{startDatePostfix}";
            var newPassword = Utility.GeneratePassword(length: 50);

            // Create new user
            var createUserResponse = await rabbitMqHttpClient.PutAsJsonAsync($"/api/users/{newUsername}", new
            {
                password = newPassword,
                tags = "" // Users for services are only for publishing and consuming messages
            });
            createUserResponse.EnsureSuccessStatusCode();

            logger.LogInformation($"Connection_RabbitMQ ({oldConnectionOptions.Username}) - A new user has been created.");

            // Copy permissions to new user
            var permissions = await rabbitMqHttpClient.GetFromJsonAsync<JsonElement>($"/api/permissions/{Uri.EscapeDataString("/")}/{oldConnectionOptions.Username}");
            var permissionsResponse = await rabbitMqHttpClient.PutAsJsonAsync($"/api/permissions/{Uri.EscapeDataString("/")}/{newUsername}", new
            {
                configure = permissions.GetProperty("configure").GetString(),
                write = permissions.GetProperty("write").GetString(),
                read = permissions.GetProperty("read").GetString()
            });
            permissionsResponse.EnsureSuccessStatusCode();

            logger.LogInformation($"Connection_RabbitMQ ({oldConnectionOptions.Username}) - The new user's permissions have been updated.");

            // Add the old user to the deletion list
            oldConnectionsToDelete.RabbitMqUsers.Add(oldConnectionOptions.Username);

            // Update old values
            oldConnectionOptions.Username = newUsername;
            oldConnectionOptions.Password = newPassword;
        }

        private async Task UpdateConnections_CreateNewRedisUserAsync(
            string startDatePostfix,
            RedisConnectionOptions oldConnectionOptions,
            OldConnections oldConnectionsToDelete,
            ILambdaLogger logger)
        {
            var newUsername = $"{oldConnectionOptions.Username.Split('_')[0]}_{startDatePostfix}";
            var newPassword = Utility.GeneratePassword(length: 50);

            // Get the old user's data
            var usersResponse = await _awsElastiCache.DescribeUsersAsync(new DescribeUsersRequest()
            {
                UserId = oldConnectionOptions.Username
            });
            var oldUser = usersResponse.Users
                .Where(u => u.UserName == oldConnectionOptions.Username)
                .Single();

            // Create a new user with the old user's permissions
            await _awsElastiCache.CreateUserAsync(new CreateUserRequest()
            {
                UserId = newUsername,
                UserName = newUsername,
                Engine = oldUser.Engine,
                AccessString = oldUser.AccessString,
                Passwords = new List<string>() { newPassword }
            });

            logger.LogInformation($"Connection_Redis ({oldConnectionOptions.Username}) - A new user has been created with permissions.");

            // Add the new user to the user group in ElastiCache
            await _awsElastiCache.ModifyUserGroupAsync(new ModifyUserGroupRequest()
            {
                UserGroupId = oldUser.UserGroupIds[0], // One ElastiCache per purpose, therefore one group per user
                UserIdsToAdd = new List<string>() { newUsername }
            });

            logger.LogInformation($"Connection_Redis ({oldConnectionOptions.Username}) - The new user has been assigned to a group.");

            // Add the old user to the deletion list
            oldConnectionsToDelete.RedisUsers.Add(new OldConnections.RedisUser()
            {
                Username = oldConnectionOptions.Username,
                UserGroupId = oldUser.UserGroupIds[0]
            });

            // Update old values and return
            oldConnectionOptions.Username = newUsername;
            oldConnectionOptions.Password = newPassword;
        }

        private async Task UpdateConnectionCredentials_WaitForUpdateTasksAsync(
            Dictionary<string, Task> updateTasks,
            Dictionary<string, ServiceRotationResult> serviceResults,
            ILambdaLogger logger)
        {
            try
            {
                await Task.WhenAll(updateTasks.Values);
            }
            catch { }

            foreach (var task in updateTasks)
            {
                if (task.Value.IsCompletedSuccessfully)
                {
                    serviceResults[task.Key].Connections.SetValues(value: true);

                    logger.LogInformation($"Connections ({task.Key}) - The connection credentials have been updated.");
                }
                else
                {
                    serviceResults[task.Key].Connections.SetValues(value: false);

                    logger.LogError($"Connections ({task.Key}) - Something went wrong. Message: {task.Value.Exception?.Message}.");
                }
            }

            updateTasks.Clear();
        }
    }
}
