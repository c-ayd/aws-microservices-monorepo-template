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
using Shared.Lambda.Structures;

namespace Lambdas.Test.Integration.SecretsRotations.Collections
{
    [CollectionDefinition(nameof(LambdaCollection))]
    public class LambdaCollection : ICollectionFixture<LambdaCollectionCluster>
    {
    }

    public class LambdaCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public static readonly string[] OldUsernames = [
            "svc1_2026-09",
            "svc2_2026-09",
            "svc3_2026-09"
        ];

        public static readonly string DefaultPassword = PasswordGenerator.GenerateValid();

        public const string PostgreSqlDbName = "test-db";
        public const string RedisGroupId = "test-group";

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

            await SetupPostgreSqlAsync(caBundle);
            await SetupRabbitMqAsync(caBundle);
            await SetupAwsAsync();
        }

        public async Task DisposeAsync()
        {
            await AwsFixture.DisposeAsync();
        }

        private async Task SetupPostgreSqlAsync(X509Certificate2Collection caBundle)
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
                        var defaultUsername = $"{oldUsername.Split('_')[0]}_default";
                        await using (var command = new NpgsqlCommand(
                            $@"CREATE ROLE ""{oldUsername}"" LOGIN PASSWORD '{DefaultPassword}';
                            GRANT ""{defaultUsername}"" TO ""{oldUsername}"";",
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
                }
            }
        }

        private async Task SetupRabbitMqAsync(X509Certificate2Collection caBundle)
        {
            var connectionOptions = RabbitMqFixture.GetConnectionOptions();
            connectionOptions.Port = RabbitMqFixture.GetMappedPort(15671);

            using var httpClient = new HttpClient(new SocketsHttpHandler()
            {
                SslOptions = new SslClientAuthenticationOptions()
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                        {
                            sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
                        }

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

        private async Task SetupAwsAsync()
        {
            var postgreSqlConnectionsOptions = PostgreSqlFixture.GetConnectionOptions("postgres");
            var rabbitMqConnectionsOptions = RabbitMqFixture.GetConnectionOptions();
            
            var oldUsers = new OldUsers();
            foreach (var oldUsername in OldUsernames)
            {
                oldUsers.PostgreSqlUsers.Add(new OldUsers.PostgreSqlUser()
                {
                    Username = oldUsername,
                    Database = PostgreSqlDbName
                });

                oldUsers.RabbitMqUsers.Add(oldUsername);

                oldUsers.RedisUsers.Add(new OldUsers.RedisUser()
                {
                    Username = oldUsername,
                    UserGroupId = RedisGroupId
                });
            }

            await AwsFixture.AwsSecretsManager.AddStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
                new
                {
                    CABundle = new
                    {
                        Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, CABundlePath))
                    }
                }));
            await AwsFixture.AwsSecretsManager.AddStringAsync("template/admin-connections", JsonSerializer.Serialize(
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
            await AwsFixture.AwsSecretsManager.AddStringAsync("template/old-users", JsonSerializer.Serialize(oldUsers));

            await AwsFixture.AwsElastiCache.CreateUserGroup(RedisGroupId);
            foreach (var oldUsername in OldUsernames)
            {
                await AwsFixture.AwsElastiCache.CreateUser(oldUsername, DefaultPassword);
                await AwsFixture.AwsElastiCache.ModifyUserGroup(
                    RedisGroupId,
                    usersToAdd: new List<string>() { oldUsername },
                    usersToRemove: null);
            }
        }
    }
}
