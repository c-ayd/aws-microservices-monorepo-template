using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Text.Json;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.Aws.Helpers.Options;
using Common.PostgreSql.Helpers.Options.Extensions;
using Common.RabbitMq.Helpers.Options.Extensions;
using Common.Redis.Helpers.Options.Extensions;
using Common.Test.Helpers.Structures;
using Lambda.SecretsRotations;
using Lambda.SecretsRotations.Utilities;
using Lambdas.Test.Integration.SecretsRotations.Collections;
using Npgsql;
using RabbitMQ.Client;
using Shared.Lambda.Structures;

namespace Lambdas.Test.Integration.SecretsRotations
{
    [Collection(nameof(LambdaCollection))]
    public class FunctionTest
    {
        private readonly LambdaCollectionCluster _collectionCluster;

        private readonly Function _lambdaFunction;

        public FunctionTest(LambdaCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;

            _lambdaFunction = new Function();
        }

        [Fact]
        public async Task FunctionHandler_WhenIsCalled_ShouldUpdateSecretsAndRestartServices()
        {
            // Arrange
            Utility.TryDeserializeJson<CertificatesOptionsBase>(
                json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc1/certificates"),
                section: CertificatesOptionsBase.Key,
                out var servic1OldCertificate);
            Utility.TryDeserializeJson<CertificatesOptionsBase>(
                json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc2/certificates"),
                section: CertificatesOptionsBase.Key,
                out var servic2OldCertificate);
            Utility.TryDeserializeJson<CertificatesOptionsBase>(
                json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc3/certificates"),
                section: CertificatesOptionsBase.Key,
                out var servic3OldCertificate);
            Utility.TryDeserializeJson<CertificatesOptionsBase>(
                json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc4/certificates"),
                section: CertificatesOptionsBase.Key,
                out var servic4OldCertificate);

            var oldCertificates = new Dictionary<string, CertificatesOptionsBase>()
            {
                { "template/svc1/certificates", servic1OldCertificate! },
                { "template/svc2/certificates", servic2OldCertificate! },
                { "template/svc3/certificates", servic3OldCertificate! },
                { "template/svc4/certificates", servic4OldCertificate! }
            };

            var oldConnections = new Dictionary<string, string>()
            {
                { "template/svc1/connections", await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc1/connections") },
                { "template/svc2/connections", await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc2/connections") },
                { "template/svc3/connections", await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc3/connections") },
                { "template/svc5/connections", await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc5/connections") }
            };

            Utility.TryDeserializeJson<CABundleOptionsBase>(
                json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/cert/ca-bundle"),
                section: CABundleOptionsBase.Key,
                out var cABundleOptions);
            var caBundle = TlsUtility.LoadCertificateChain(cABundleOptions!.Value);

            var input = JsonSerializer.Serialize(new
            {
                detail = new EventJson()
                {
                    EcsClusterName = "template-ecs-cluster",
                    JwtKeysSecretId = "template/jwt",
                    Certificate = new EventJson.CertificateInfo()
                    {
                        IntermediateCASecretId = "template/cert/intermediate",
                        CABundleSecretId = "template/cert/ca-bundle",
                        Organization = "localhost",
                        Country = "DE"
                    },
                    Connection = new EventJson.ConnectionInfo()
                    {
                        AdminConnectionsSecretId = "template/admin-connections",
                        OldUsersSecretId = "template/old-users"
                    },
                    Services = new List<EventJson.ServiceInfo>()
                    {
                        new EventJson.ServiceInfo()
                        {
                            ServiceName = "Service1",
                            Certificate = new EventJson.ServiceInfo.CertificateUpdateInfo()
                            {
                                CertificateSecretId = "template/svc1/certificates",
                                DnsNames = new List<string>() { "localhost", "127.0.0.1" }
                            },
                            Connection = new EventJson.ServiceInfo.ConnectionUpdateInfo()
                            {
                                ConncetionsSecretId = "template/svc1/connections"
                            }
                        },
                        new EventJson.ServiceInfo()
                        {
                            ServiceName = "Service2",
                            Certificate = new EventJson.ServiceInfo.CertificateUpdateInfo()
                            {
                                CertificateSecretId = "template/svc2/certificates",
                                DnsNames = new List<string>() { "localhost", "127.0.0.1" }
                            },
                            Connection = new EventJson.ServiceInfo.ConnectionUpdateInfo()
                            {
                                ConncetionsSecretId = "template/svc2/connections"
                            }
                        },
                        new EventJson.ServiceInfo()
                        {
                            ServiceName = "Service3",
                            Certificate = new EventJson.ServiceInfo.CertificateUpdateInfo()
                            {
                                CertificateSecretId = "template/svc3/certificates",
                                DnsNames = new List<string>() { "localhost", "127.0.0.1" }
                            },
                            Connection = new EventJson.ServiceInfo.ConnectionUpdateInfo()
                            {
                                ConncetionsSecretId = "template/svc3/connections"
                            }
                        },
                        new EventJson.ServiceInfo()
                        {
                            ServiceName = "Service4",
                            Certificate = new EventJson.ServiceInfo.CertificateUpdateInfo()
                            {
                                CertificateSecretId = "template/svc4/certificates",
                                DnsNames = new List<string>() { "localhost", "127.0.0.1" }
                            }
                        },
                        new EventJson.ServiceInfo()
                        {
                            ServiceName = "Service5",
                            Connection = new EventJson.ServiceInfo.ConnectionUpdateInfo()
                            {
                                ConncetionsSecretId = "template/svc5/connections"
                            }
                        }
                    },
                    Environment = new EventJson.EnvironmentInfo()
                    {
                        IsProduction = false,
                        ServiceUrl = $"http://localhost:{_collectionCluster.AwsFixture.GetMappedPort()}"
                    }
                }
            });

            // Act
            var lambdaResult = await _lambdaFunction.FunctionHandler(input, new LambdaContextTest());

            // Assert
            Assert.Equal(HttpStatusCode.OK, (HttpStatusCode)lambdaResult["statusCode"]);

            var jwtKeysSecret = await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/jwt");
            Utility.TryDeserializeJson<JwtKeysOptionsBase>(
                json: jwtKeysSecret,
                section: JwtKeysOptionsBase.Key,
                out var jwtKeys);
            Assert.NotEqual("v2", jwtKeys!.CurrentKeyId);
            Assert.Equal(2, jwtKeys.Keys.Count);
            foreach (var key in jwtKeys.Keys)
            {
                // Should not throw
                using var rsa = RSA.Create();
                rsa.ImportFromPem(key.PublicKey);
                rsa.ImportFromPem(key.PrivateKey);
            }

            var oldUsersToDelete = JsonSerializer.Deserialize<OldUsers>(
                await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/old-users"))!;

            foreach (var oldConnection in oldConnections)
            {
                using var jsonDocForOldConnections = JsonDocument.Parse(oldConnection.Value);
                var connectionsJsonProperty = jsonDocForOldConnections.RootElement.GetProperty("Connections");

                // PostgreSQL
                if (connectionsJsonProperty.TryGetProperty("PostgreSql", out var postgreSqlConnectionJson))
                {
                    var oldPostgreSqlConnectionOptions = JsonSerializer.Deserialize<PostgreSqlConnectionOptions>(postgreSqlConnectionJson.GetRawText())!;

                    using var jsonDocForNewPostgreSql = JsonDocument.Parse(await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync(oldConnection.Key));
                    var newPostgreSqlConnectionOptions = JsonSerializer.Deserialize<PostgreSqlConnectionOptions>(
                        jsonDocForNewPostgreSql.RootElement.GetProperty("Connections").GetProperty("PostgreSql").GetRawText())!;

                    Assert.NotEqual(oldPostgreSqlConnectionOptions.Username, newPostgreSqlConnectionOptions.Username);
                    Assert.Contains(oldUsersToDelete.PostgreSqlUsers, u => u.Username == oldPostgreSqlConnectionOptions.Username);

                    var connStringBuilder = new NpgsqlConnectionStringBuilder()
                    {
                        Host = newPostgreSqlConnectionOptions.Host,
                        Port = newPostgreSqlConnectionOptions.Port,
                        Database = newPostgreSqlConnectionOptions.Database,
                        Username = newPostgreSqlConnectionOptions.Username,
                        Password = newPostgreSqlConnectionOptions.Password,
                        SslMode = SslMode.Require
                    };

                    var builder = new NpgsqlDataSourceBuilder(connStringBuilder.ConnectionString);
                    builder.UseSslClientAuthenticationOptionsCallback(options =>
                    {
                        options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                        options.TargetHost = newPostgreSqlConnectionOptions.Ssl.ServerName;
                        options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                        {
                            return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                        };
                    });

                    await using var dataSource = builder.Build();
                    await using var postgreSqlConnection = await dataSource.OpenConnectionAsync(); // Should not throw
                }
                
                // RabbitMQ
                if (connectionsJsonProperty.TryGetProperty("RabbitMq", out var rabbitMqConnectionJson))
                {
                    var oldRabbitMqConnectionOptions = JsonSerializer.Deserialize<RabbitMqConnectionOptions>(rabbitMqConnectionJson.GetRawText())!;

                    using var jsonDocForNewRabbitMq = JsonDocument.Parse(await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync(oldConnection.Key));
                    var newRabbitMqConnectionOptions = JsonSerializer.Deserialize<RabbitMqConnectionOptions>(
                        jsonDocForNewRabbitMq.RootElement.GetProperty("Connections").GetProperty("RabbitMq").GetRawText())!;

                    Assert.NotEqual(oldRabbitMqConnectionOptions.Username, newRabbitMqConnectionOptions.Username);
                    Assert.Contains(oldRabbitMqConnectionOptions.Username, oldUsersToDelete.RabbitMqUsers);

                    var connectionFactory = new ConnectionFactory()
                    {
                        HostName = newRabbitMqConnectionOptions.Host,
                        Port = newRabbitMqConnectionOptions.Port,
                        UserName = newRabbitMqConnectionOptions.Username,
                        Password = newRabbitMqConnectionOptions.Password,
                        AutomaticRecoveryEnabled = false,
                        TopologyRecoveryEnabled = false,
                        Ssl = new SslOption()
                        {
                            Enabled = true,
                            Version = SslProtocols.Tls12 | SslProtocols.Tls13,
                            AcceptablePolicyErrors = SslPolicyErrors.None,
                            ServerName = newRabbitMqConnectionOptions.Ssl.ServerName!,
                            CertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                            {
                                return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                            }
                        }
                    };

                    await using var rabbitMqConnection = await connectionFactory.CreateConnectionAsync(); // Should not throw
                }
                
                // Redis
                if (connectionsJsonProperty.TryGetProperty("Redis", out var redisConnectionJson))
                {
                    var oldRedisConnectionOptions = JsonSerializer.Deserialize<RedisConnectionOptions>(redisConnectionJson.GetRawText())!;

                    using var jsonDocForNewRedis = JsonDocument.Parse(await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync(oldConnection.Key));
                    var newRedisConnectionOptions = JsonSerializer.Deserialize<RedisConnectionOptions>(
                        jsonDocForNewRedis.RootElement.GetProperty("Connections").GetProperty("Redis").GetRawText())!;

                    Assert.NotEqual(oldRedisConnectionOptions.Username, newRedisConnectionOptions.Username);
                    Assert.Contains(oldUsersToDelete.RedisUsers, u => u.Username == oldRedisConnectionOptions.Username);

                    var newRedisUser = await _collectionCluster.AwsFixture.AwsElastiCache.GetUser(newRedisConnectionOptions.Username);
                    Assert.NotNull(newRedisUser);
                    Assert.Equal(newRedisConnectionOptions.Username, newRedisUser.UserName);
                }
            }

            foreach (var oldCertificate in oldCertificates)
            {
                Utility.TryDeserializeJson<CertificatesOptionsBase>(
                    json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync(oldCertificate.Key),
                    section: CertificatesOptionsBase.Key,
                    out var newCertificates);

                Assert.NotEqual(oldCertificate.Value.ServerCertificate, newCertificates!.ServerCertificate);
                Assert.NotEqual(oldCertificate.Value.ServerPrivateKey, newCertificates.ServerPrivateKey);
                TlsUtility.LoadCertificate(newCertificates.ServerCertificate, newCertificates.ServerPrivateKey); // Should not throw
            }
        }
    }
}
