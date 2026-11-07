using System.Net.Http.Json;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Common.AspNetCore.Helpers.Kestrel.Options.Extensions;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.PostgreSql.Helpers.Options.Extensions;
using Common.RabbitMq.Helpers.Options.Extensions;
using Common.Redis.Helpers.Options.Extensions;
using Common.Test.Generators;
using Common.Test.Helpers.Fixtures;
using Microsoft.Net.Http.Headers;
using Npgsql;

namespace Lambdas.Test.Integration.SecretsRotations.Collections
{
    [CollectionDefinition(nameof(LambdaCollection))]
    public class LambdaCollection : ICollectionFixture<LambdaCollectionCluster>
    {
    }

    public class LambdaCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        private const string _intermediateCertificatePath = "Certs/test-intermediate-ca.crt";
        private const string _intermediateCertificatePrivateKeyPath = "Certs/test-intermediate-ca.key";
        private const string _webApiCertificatePath = "Certs/WebApi/test-webapi-server.crt";
        private const string _webApiCertificatePrivateKeyPath = "Certs/WebApi/test-webapi-server.key";

        public static readonly string[] OldUsernames = ["svc1_2026-09", "svc2_2026-09"];
        public static readonly string DefaultPassword = PasswordGenerator.GenerateValid();

        public const string PostgreSqlDbName = "test-db";

        public AwsFixture AwsFixture { get; private set; }
        public PostgreSqlFixture PostgreSqlFixture { get; private set; }
        public RabbitMqFixture RabbitMqFixture { get; private set; }

        public LambdaCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            PostgreSqlFixture = new PostgreSqlFixture();
            RabbitMqFixture = new RabbitMqFixture();
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                AwsFixture.InitializeAsync(),
                PostgreSqlFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    serverCertificatePath: "Certs/PostgreSql/test-postgresql-server.crt",
                    serverCertificatePrivateKeyPath: "Certs/PostgreSql/test-postgresql-server.key"),
                RabbitMqFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq")
            );

            var caBundle = TlsUtility.LoadCertificateChain(
                await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, CABundlePath)));

            await SetupPostgreSql(caBundle);
            await SetupRabbitMq(caBundle);
            await SetupAws();
        }

        public async Task DisposeAsync()
        {
            await AwsFixture.DisposeAsync();
        }

        private async Task SetupPostgreSql(X509Certificate2Collection caBundle)
        {
            var defaultUsernames = new List<string>();
            for (int i = 0; i < OldUsernames.Length; ++i)
            {
                var baseUsername = OldUsernames[i].Split('_')[0];
                defaultUsernames.Add($"{baseUsername}_default");
            }

            var connectionOptions = PostgreSqlFixture.GetConnectionOptions("postgres");
            var connStringBuilder = new NpgsqlConnectionStringBuilder()
            {
                Host = connectionOptions.Host,
                Port = connectionOptions.Port,
                Database = connectionOptions.Database,
                Username = connectionOptions.Username,
                Password = connectionOptions.Password,
                SslMode = SslMode.Require
            };

            var builder = new NpgsqlDataSourceBuilder(connStringBuilder.ConnectionString);
            builder.UseSslClientAuthenticationOptionsCallback(options =>
            {
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                options.TargetHost = connectionOptions.Ssl.ServerName;
                options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                };
            });

            await using (var dataSource = builder.Build())
            {
                await using (var connection = await dataSource.OpenConnectionAsync())
                {
                    await using (var command = new NpgsqlCommand(
                        $"CREATE DATABASE \"{PostgreSqlDbName}\";",
                        connection))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    foreach (var defaultUsername in defaultUsernames)
                    {
                        await using (var command = new NpgsqlCommand(
                            $@"CREATE ROLE ""{defaultUsername}"" LOGIN PASSWORD '{DefaultPassword}';
                            GRANT CONNECT ON DATABASE ""{PostgreSqlDbName}"" TO ""{defaultUsername}"";",
                            connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    foreach (var oldUsername in OldUsernames)
                    {
                        await using (var command = new NpgsqlCommand(
                            $@"CREATE ROLE ""{oldUsername}"" LOGIN PASSWORD '{DefaultPassword}';
                            GRANT CONNECT ON DATABASE ""{PostgreSqlDbName}"" TO ""{oldUsername}"";",
                            connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
            }

            connStringBuilder = new NpgsqlConnectionStringBuilder(connStringBuilder.ConnectionString)
            {
                Database = PostgreSqlDbName,
            };

            builder = new NpgsqlDataSourceBuilder(connStringBuilder.ConnectionString);
            builder.UseSslClientAuthenticationOptionsCallback(options =>
            {
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                options.TargetHost = connectionOptions.Ssl.ServerName;
                options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                };
            });

            await using (var dataSource = builder.Build())
            {
                await using (var connection = await dataSource.OpenConnectionAsync())
                {
                    foreach (var defaultUsername in defaultUsernames)
                    {
                        await using (var command = new NpgsqlCommand(
                            $@"GRANT USAGE ON SCHEMA public TO ""{defaultUsername}"";
                            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO ""{defaultUsername}"";
                            GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO ""{defaultUsername}"";
                            ALTER DEFAULT PRIVILEGES FOR ROLE ""{connectionOptions.Username}"" IN SCHEMA public
                                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO ""{defaultUsername}"";
                            ALTER DEFAULT PRIVILEGES FOR ROLE ""{connectionOptions.Username}"" IN SCHEMA public
                                GRANT USAGE, SELECT ON SEQUENCES TO ""{defaultUsername}"";",
                            connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                    }

                    foreach (var oldUsername in OldUsernames)
                    {
                        var defaultUsername = $"{oldUsername.Split('_')[0]}_default";
                        await using (var command = new NpgsqlCommand(
                            $"GRANT \"{defaultUsername}\" TO \"{oldUsername}\";",
                            connection))
                        {
                            await command.ExecuteNonQueryAsync();
                        }
                    }
                }
            }
        }

        private async Task SetupRabbitMq(X509Certificate2Collection caBundle)
        {
            var connectionOptions = RabbitMqFixture.GetConnectionOptions();
            connectionOptions.Port = RabbitMqFixture.GetMappedPort(15671);

            using var httpClient = new HttpClient(new SocketsHttpHandler()
            {
                SslOptions = new SslClientAuthenticationOptions()
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                    }
                },
            });
            httpClient.BaseAddress = new Uri($"https://{connectionOptions.Host}:{connectionOptions.Port}");
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Basic " +
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connectionOptions.Username}:{connectionOptions.Password}")));

            foreach (var oldUsername in OldUsernames)
            {
                var createUserResponse = await httpClient.PutAsJsonAsync($"/api/users/{oldUsername}", new
                {
                    username = oldUsername,
                    password = DefaultPassword,
                    tags = ""
                });
                createUserResponse.EnsureSuccessStatusCode();

                var permissionResponse = await httpClient.PutAsJsonAsync($"/api/permissions/{Uri.EscapeDataString("/")}/{oldUsername}/", new
                {
                    configure = "^test\\.",
                    write = "^test\\.",
                    read = "^test\\."
                });
                permissionResponse.EnsureSuccessStatusCode();
            }
        }

        private async Task SetupAws()
        {
            var postgreSqlConnectionsOptions = PostgreSqlFixture.GetConnectionOptions("postgres");
            var rabbitMqConnectionsOptions = RabbitMqFixture.GetConnectionOptions();
            var redisConnectionsOptions = new RedisConnectionOptions()
            {
                Host = "localhost",
                Port = 6380,
                Username = OldUsernames[0],
                Password = DefaultPassword,
                AbortOnConnectionFail = true,
                Ssl = new SslOptions()
                {
                    Enabled = true,
                    ServerName = "localhost"
                }
            };

            await AwsFixture.SecretsManager_AddStringAsync("template/jwt", JsonSerializer.Serialize(
                new
                {
                    JwtKeys = new
                    {
                        CurrentKeyId = "v2",
                        Keys = new[]
                        {
                            new
                            {
                                KeyId = "v1",
                                ExpirationTime = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                                NotBeforeTime = DateTimeOffset.UtcNow.AddMonths(-3).ToUnixTimeSeconds(),
                                PrivateKey = File.ReadAllText("./test-jwt-v1-private.pem"),
                                PublicKey = File.ReadAllText("./test-jwt-v1-public.pem")
                            },
                            new
                            {
                                KeyId = "v2",
                                ExpirationTime = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds(),
                                NotBeforeTime = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                                PrivateKey = File.ReadAllText("./test-jwt-v2-private.pem"),
                                PublicKey = File.ReadAllText("./test-jwt-v2-public.pem")
                            }
                        }
                    }
                }
            ));
            await AwsFixture.SecretsManager_AddStringAsync("template/svc1/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        PostgreSql = new PostgreSqlConnectionOptions()
                        {
                            Host = postgreSqlConnectionsOptions.Host,
                            Port = postgreSqlConnectionsOptions.Port,
                            Database = PostgreSqlDbName,
                            Username = OldUsernames[0],
                            Password = DefaultPassword,
                            Ssl = postgreSqlConnectionsOptions.Ssl
                        },
                        RabbitMq = new RabbitMqConnectionOptions()
                        {
                            Host = rabbitMqConnectionsOptions.Host,
                            Port = rabbitMqConnectionsOptions.Port,
                            Username = OldUsernames[0],
                            Password = DefaultPassword,
                            Ssl = rabbitMqConnectionsOptions.Ssl
                        },
                        Redis = new RedisConnectionOptions()
                        {
                            Host = redisConnectionsOptions.Host,
                            Port = redisConnectionsOptions.Port,
                            Username = OldUsernames[0],
                            Password = DefaultPassword,
                            AbortOnConnectionFail = redisConnectionsOptions.AbortOnConnectionFail,
                            Ssl = redisConnectionsOptions.Ssl
                        }
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/svc2/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        PostgreSql = new PostgreSqlConnectionOptions()
                        {
                            Host = postgreSqlConnectionsOptions.Host,
                            Port = postgreSqlConnectionsOptions.Port,
                            Database = PostgreSqlDbName,
                            Username = OldUsernames[1],
                            Password = DefaultPassword,
                            Ssl = postgreSqlConnectionsOptions.Ssl
                        },
                        RabbitMq = new RabbitMqConnectionOptions()
                        {
                            Host = rabbitMqConnectionsOptions.Host,
                            Port = rabbitMqConnectionsOptions.Port,
                            Username = OldUsernames[1],
                            Password = DefaultPassword,
                            Ssl = rabbitMqConnectionsOptions.Ssl
                        },
                        Redis = new RedisConnectionOptions()
                        {
                            Host = redisConnectionsOptions.Host,
                            Port = redisConnectionsOptions.Port,
                            Username = OldUsernames[1],
                            Password = DefaultPassword,
                            AbortOnConnectionFail = redisConnectionsOptions.AbortOnConnectionFail,
                            Ssl = redisConnectionsOptions.Ssl
                        }
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/svc1/certificates", JsonSerializer.Serialize(
                new
                {
                    Certificates = new
                    {
                        ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _webApiCertificatePath)),
                        ServerPrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _webApiCertificatePrivateKeyPath))
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/svc2/certificates", JsonSerializer.Serialize(
                new
                {
                    Certificates = new
                    {
                        ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _webApiCertificatePath)),
                        ServerPrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _webApiCertificatePrivateKeyPath))
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/svc3/certificates", JsonSerializer.Serialize(
                new
                {
                    Certificates = new
                    {
                        ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _webApiCertificatePath)),
                        ServerPrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _webApiCertificatePrivateKeyPath))
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/cert/intermediate", JsonSerializer.Serialize(
                new
                {
                    Certificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _intermediateCertificatePath)),
                    PrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _intermediateCertificatePrivateKeyPath)),
                    Password = "int"
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
                new
                {
                    CABundle = new
                    {
                        Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, CABundlePath))
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/admin-connections", JsonSerializer.Serialize(
                new
                {
                    PostgreSql = postgreSqlConnectionsOptions,
                    RabbitMq = new RabbitMqConnectionOptions()
                    {
                        Host = rabbitMqConnectionsOptions.Host,
                        Port = RabbitMqFixture.GetMappedPort(15671),
                        Username = rabbitMqConnectionsOptions.Username,
                        Password = rabbitMqConnectionsOptions.Password,
                        Ssl = new SslOptions()
                        {
                            Enabled = true,
                            ServerName = "localhost"
                        }
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/old-connection-credentials", JsonSerializer.Serialize("{}"));

            await AwsFixture.ElastiCache_CreateUserGroup(groupName: "test-group");
            foreach (var oldUsername in OldUsernames)
            {
                await AwsFixture.ElastiCache_CreateUser(oldUsername, DefaultPassword);
                await AwsFixture.ElastiCache_ModifyUserGroup(
                    groupName: "test-group",
                    usersToAdd: new List<string>() { oldUsername },
                    usersToRemove: null);
            }

            await AwsFixture.Ecs_CreateClusterAsync(clusterName: "template-ecs-cluster");
            await AwsFixture.Ecs_RegisterTaskDefiniton(taskName: "test-task-definition");
            await AwsFixture.Ecs_CreateServiceAsync(
                clusterName: "template-ecs-cluster",
                taskName: "test-task-definition",
                serviceName: "Service1");
            await AwsFixture.Ecs_CreateServiceAsync(
                clusterName: "template-ecs-cluster",
                taskName: "test-task-definition",
                serviceName: "Service2");
            await AwsFixture.Ecs_CreateServiceAsync(
                clusterName: "template-ecs-cluster",
                taskName: "test-task-definition",
                serviceName: "Service3");
        }
    }
}
