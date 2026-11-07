using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
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
        private async Task UpdateConnectionCredentials(
            BatchGetSecretValueResponse secrets,
            EventJson.ConnectionsInfo connectionsInfo,
            DateTimeOffset startDate,
            Dictionary<string, ServiceRotationResult> results,
            ILambdaLogger logger)
        {
            var startDatePostfix = startDate.ToString("yyyy-MM");
            var oldConnectionCredentialsToDelete = new OldConnectionCredentials();

            // Deserialize secrets required for connecting infrastructure services and TLS handshake
            var adminConnections = JsonSerializer.Deserialize<AdminConnections>(secrets.SecretValues
                .Where(s => s.Name == connectionsInfo.AdminConnectionsSecretId)
                .FirstOrDefault()!
                .SecretString)!;
            var caBundleOptions = Utility.GetOptionsFromJson<CABundleOptionsBase>(
                json: secrets.SecretValues
                .Where(s => s.Name == connectionsInfo.CABundleSecretId)
                .FirstOrDefault()!
                .SecretString,
                elementName: CABundleOptionsBase.Key)!;
            var caBundle = TlsUtility.LoadCertificateChain(caBundleOptions.Value);

            // Create HTTP client for RabbitMQ
            using var rabbitMqHttpClient = new HttpClient(new SocketsHttpHandler()
            {
                SslOptions = new SslClientAuthenticationOptions()
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                    }
                },
            });
            rabbitMqHttpClient.BaseAddress = new Uri($"https://{adminConnections.RabbitMq.Host}:{adminConnections.RabbitMq.Port}");
            rabbitMqHttpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Basic " +
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{adminConnections.RabbitMq.Username}:{adminConnections.RabbitMq.Password}")));

            // Update connection credentials
            var updateTasks = new Dictionary<string, Task>();
            foreach (var service in connectionsInfo.Services)
            {
                using var jsonDoc = JsonDocument.Parse(secrets.SecretValues
                    .Where(s => s.Name == service.ConncetionsSecretId)
                    .FirstOrDefault()!
                    .SecretString);

                var newConnectionsOptions = new Dictionary<string, object>();
                var oldConnectionsOptions = jsonDoc.RootElement.GetProperty(ConnectionsOptionsBase.Key);
                foreach (var property in oldConnectionsOptions.EnumerateObject())
                {
                    var json = property.Value.GetRawText();
                    if (Utility.TryDeserializeJson(json, out PostgreSqlConnectionOptions? postgreSqlConnectionOptions))
                    {
                        try
                        {
                            await UpdateConnectionCredentials_CreateNewPostgresqlCredentialsAsync(
                                adminConnections.PostgreSql,
                                caBundle,
                                startDatePostfix,
                                postgreSqlConnectionOptions!,
                                oldConnectionCredentialsToDelete,
                                logger);

                            newConnectionsOptions.Add(property.Name, postgreSqlConnectionOptions!);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, $"Connection_PostgreSQL ({postgreSqlConnectionOptions!.Username}) - Something went wrong. Message {exception.Message}.");
                        }
                    }
                    else if (Utility.TryDeserializeJson(json, out RabbitMqConnectionOptions? rabbitMqConnectionOptions))
                    {
                        try
                        {
                            await UpdateConnectionCredentials_CreateNewRabbitMqCredentialsAsync(
                                rabbitMqHttpClient,
                                startDatePostfix,
                                rabbitMqConnectionOptions!,
                                oldConnectionCredentialsToDelete,
                                logger);

                            newConnectionsOptions.Add(property.Name, rabbitMqConnectionOptions!);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, $"Connection_RabbitMQ ({rabbitMqConnectionOptions!.Username}) - Something went wrong. Message {exception.Message}.");
                        }
                    }
                    else if (Utility.TryDeserializeJson(json, out RedisConnectionOptions? redisConnectionOptions))
                    {
                        try
                        {
                            await UpdateConnectionCredentials_CreateNewRedisCredentialsAsync(
                                startDatePostfix,
                                redisConnectionOptions!,
                                oldConnectionCredentialsToDelete,
                                logger);

                            newConnectionsOptions.Add(property.Name, redisConnectionOptions!);
                        }
                        catch (Exception exception)
                        {
                            logger.LogError(exception, $"Connection_Redis ({redisConnectionOptions!.Username}) - Something went wrong. Message {exception.Message}.");
                        }
                    }
                    else
                    {
                        logger.LogCritical($"Unknown connection type. Service Name: {service.ServiceName}, JSON Key: {property.Name}.");
                    }
                }

                // Add task to the list for batch update
                updateTasks.Add(service.ServiceName, AwsSecretsManager.PutSecretValueAsync(new PutSecretValueRequest()
                {
                    SecretId = service.ConncetionsSecretId,
                    SecretString = JsonSerializer.Serialize(new Dictionary<string, object>()
                    {
                        { ConnectionsOptionsBase.Key, newConnectionsOptions }
                    })
                }));

                // Wait for the batch to be completed before creating more credentials
                if (updateTasks.Count >= _batchSize)
                {
                    await UpdateConnectionCredentials_WaitForUpdateTasksAsync(updateTasks, results, logger);
                }
            }

            // Wait for the last batch if there is any
            if (updateTasks.Count != 0)
            {
                await UpdateConnectionCredentials_WaitForUpdateTasksAsync(updateTasks, results, logger);
            }

            // Save the old connection credentials to AWS Secrets Manager to delete them later in another lambda fuction
            await AwsSecretsManager.PutSecretValueAsync(new PutSecretValueRequest()
            {
                SecretId = connectionsInfo.OldConnectionCredentialsSecretId,
                SecretString = JsonSerializer.Serialize(oldConnectionCredentialsToDelete)
            });

            logger.LogInformation("Connection_FINAL - The old connection credentials are saved to AWS Secrets Manager.");
        }

        private async Task UpdateConnectionCredentials_CreateNewPostgresqlCredentialsAsync(
            PostgreSqlConnectionOptions adminConnectionOptions,
            X509Certificate2Collection caBundle,
            string startDatePostfix,
            PostgreSqlConnectionOptions oldConnectionOptions,
            OldConnectionCredentials oldConnectionCredentialsToDelete,
            ILambdaLogger logger)
        {
            var baseUsername = oldConnectionOptions.Username.Split('_')[0];
            var newUsername = $"{baseUsername}_{startDatePostfix}";
            var newPassword = Utility.GeneratePassword(length: 50);

            // Connect to DB
            var connStringBuilder = new NpgsqlConnectionStringBuilder()
            {
                Host = adminConnectionOptions.Host,
                Port = adminConnectionOptions.Port,
                Database = oldConnectionOptions.Database,
                Username = adminConnectionOptions.Username,
                Password = adminConnectionOptions.Password,
                SslMode = SslMode.Require
            };

            var builder = new NpgsqlDataSourceBuilder(connStringBuilder.ConnectionString);
            builder.UseSslClientAuthenticationOptionsCallback(options =>
            {
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                options.TargetHost = adminConnectionOptions.Ssl.ServerName;
                options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                };
            });

            await using var dataSource = builder.Build();
            await using var connection = await dataSource.OpenConnectionAsync();

            // Create a new user and copy the permissions from the template user
            await using var transaction = await connection.BeginTransactionAsync();

            await using (var command = new NpgsqlCommand(
                $@"CREATE ROLE ""{newUsername}"" LOGIN PASSWORD '{newPassword}';
                GRANT ""{baseUsername}_default"" TO ""{newUsername}"";",
                connection, transaction))
            {
                await command.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();

            logger.LogInformation($"Connection_PostgreSQL ({oldConnectionOptions.Username}) - A new user has been created with permissions.");

            // Add the old connection credentials to the deletion list
            oldConnectionCredentialsToDelete.PostgreSqlUsers.Add(new OldConnectionCredentials.PostgreSqlUser()
            {
                Username = oldConnectionOptions.Username,
                Database = oldConnectionOptions.Database
            });

            // Update old values
            oldConnectionOptions.Username = newUsername;
            oldConnectionOptions.Password = newPassword;
        }

        private async Task UpdateConnectionCredentials_CreateNewRabbitMqCredentialsAsync(
            HttpClient rabbitMqHttpClient,
            string startDatePostfix,
            RabbitMqConnectionOptions oldConnectionOptions,
            OldConnectionCredentials oldConnectionCredentialsToDelete,
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

            // Copy old permissions to new user
            var oldPermissions = await rabbitMqHttpClient.GetFromJsonAsync<JsonElement>($"/api/permissions/{Uri.EscapeDataString("/")}/{oldConnectionOptions.Username}");
            var permissionsResponse = await rabbitMqHttpClient.PutAsJsonAsync($"/api/permissions/{Uri.EscapeDataString("/")}/{newUsername}", new
            {
                configure = oldPermissions.GetProperty("configure").GetString(),
                write = oldPermissions.GetProperty("write").GetString(),
                read = oldPermissions.GetProperty("read").GetString()
            });
            permissionsResponse.EnsureSuccessStatusCode();

            logger.LogInformation($"Connection_RabbitMQ ({oldConnectionOptions.Username}) - The new user's permissions have been updated.");

            // Add the old connection credentials to the deletion list
            oldConnectionCredentialsToDelete.RabbitMqUsers.Add(oldConnectionOptions.Username);

            // Update old values
            oldConnectionOptions.Username = newUsername;
            oldConnectionOptions.Password = newPassword;
        }

        private async Task UpdateConnectionCredentials_CreateNewRedisCredentialsAsync(
            string startDatePostfix,
            RedisConnectionOptions oldConnectionOptions,
            OldConnectionCredentials oldConnectionCredentialsToDelete,
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

            // Add the old connection credentials to the deletion list
            oldConnectionCredentialsToDelete.RedisUsers.Add(new OldConnectionCredentials.RedisUser()
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
            Dictionary<string, ServiceRotationResult> results,
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
                    if (results.ContainsKey(task.Key))
                    {
                        results[task.Key].Connections = true;
                    }
                    else
                    {
                        results.Add(task.Key, new ServiceRotationResult()
                        {
                            Connections = true
                        });
                    }

                    logger.LogInformation($"Connections ({task.Key}) - The connection credentials have been updated.");
                }
                else
                {
                    if (results.ContainsKey(task.Key))
                    {
                        results[task.Key].Connections = false;
                    }
                    else
                    {
                        results.Add(task.Key, new ServiceRotationResult()
                        {
                            Connections = false
                        });
                    }

                    logger.LogError($"Connections ({task.Key}) - Something went wrong. Message: {task.Value.Exception?.Message}, Stack Trace: {task.Value.Exception?.StackTrace}");
                }
            }

            updateTasks.Clear();
        }
    }
}
