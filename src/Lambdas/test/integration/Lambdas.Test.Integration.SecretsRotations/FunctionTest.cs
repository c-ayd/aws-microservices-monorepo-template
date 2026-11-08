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
using Lambda.SecretsRotations.Structures;
using Lambda.SecretsRotations.Utilities;
using Lambdas.Test.Integration.SecretsRotations.Collections;
using Npgsql;
using RabbitMQ.Client;

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
            var oldCertificates = new Dictionary<string, CertificatesOptionsBase>()
            {
                { "template/svc1/certificates", Utility.GetOptionsFromJson<CertificatesOptionsBase>(
                    json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc1/certificates"),
                    elementName: CertificatesOptionsBase.Key)! },
                { "template/svc2/certificates", Utility.GetOptionsFromJson<CertificatesOptionsBase>(
                    json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc2/certificates"),
                    elementName: CertificatesOptionsBase.Key)! },
                { "template/svc3/certificates", Utility.GetOptionsFromJson<CertificatesOptionsBase>(
                    json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc3/certificates"),
                    elementName: CertificatesOptionsBase.Key)! }
            };

            var oldConnections = new Dictionary<string, string>()
            {
                { "template/svc1/connections", await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc1/connections") },
                { "template/svc2/connections", await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/svc2/connections") }
            };

            var cABundleOptions = Utility.GetOptionsFromJson<CABundleOptionsBase>(
                    json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/cert/ca-bundle"),
                    elementName: CABundleOptionsBase.Key)!;
            var caBundle = TlsUtility.LoadCertificateChain(cABundleOptions.Value);

            var input = JsonSerializer.Serialize(new
            {
                detail = new EventJson()
                {
                    EcsClusterName = "template-ecs-cluster",
                    JwtKeysSecretId = "template/jwt",
                    Certificates = new EventJson.CertificatesInfo()
                    {
                        IntermediateCASecretId = "template/cert/intermediate",
                        Organization = "localhost",
                        Country = "DE",
                        Services = new List<EventJson.CertificatesInfo.ServiceInfo>()
                        {
                            new EventJson.CertificatesInfo.ServiceInfo()
                            {
                                CertificateSecretId = "template/svc1/certificates",
                                ServiceName = "Service1",
                                DnsNames = new List<string>() { "localhost", "127.0.0.1" }
                            },
                            new EventJson.CertificatesInfo.ServiceInfo()
                            {
                                CertificateSecretId = "template/svc2/certificates",
                                ServiceName = "Service2",
                                DnsNames = new List<string>() { "localhost", "127.0.0.1" }
                            },
                            new EventJson.CertificatesInfo.ServiceInfo()
                            {
                                CertificateSecretId = "template/svc3/certificates",
                                ServiceName = "Service3",
                                DnsNames = new List<string>() { "localhost", "127.0.0.1" }
                            }
                        }
                    },
                    Connections = new EventJson.ConnectionsInfo()
                    {
                        CABundleSecretId = "template/cert/ca-bundle",
                        AdminConnectionsSecretId = "template/admin-connections",
                        OldConnectionCredentialsSecretId = "template/old-connection-credentials",
                        Services = new List<EventJson.ConnectionsInfo.ServiceInfo>()
                        {
                            new EventJson.ConnectionsInfo.ServiceInfo()
                            {
                                ConncetionsSecretId = "template/svc1/connections",
                                ServiceName = "Service1"
                            },
                            new EventJson.ConnectionsInfo.ServiceInfo()
                            {
                                ConncetionsSecretId = "template/svc2/connections",
                                ServiceName = "Service2"
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
            await _lambdaFunction.FunctionHandler(input, new LambdaContextTest());

            // Assert
            var jwtKeysSecret = await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/jwt");
            var jwtKeys = Utility.GetOptionsFromJson<JwtKeysOptionsBase>(
                json: jwtKeysSecret,
                elementName: JwtKeysOptionsBase.Key)!;
            Assert.NotEqual("v2", jwtKeys.CurrentKeyId);
            Assert.Equal(2, jwtKeys.Keys.Count);
            foreach (var key in jwtKeys.Keys)
            {
                // Should not throw
                using var rsa = RSA.Create();
                rsa.ImportFromPem(key.PublicKey);
                rsa.ImportFromPem(key.PrivateKey);
            }

            var oldConnectionCredentials = JsonSerializer.Deserialize<OldConnectionCredentials>(
                await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/old-connection-credentials"))!;

            foreach (var oldUsername in LambdaCollectionCluster.OldUsernames)
            {
                Assert.Contains(oldConnectionCredentials.PostgreSqlUsers, u => u.Username == oldUsername);
                Assert.Contains(oldUsername, oldConnectionCredentials.RabbitMqUsers);
                Assert.Contains(oldConnectionCredentials.RedisUsers, u => u.Username == oldUsername);
            }

            foreach (var oldConnection in oldConnections)
            {
                using var jsonDocForOldConnections = JsonDocument.Parse(oldConnection.Value);

                // PostgreSQL
                var oldPostgreSqlConnectionOptions = JsonSerializer.Deserialize<PostgreSqlConnectionOptions>(
                    jsonDocForOldConnections.RootElement.GetProperty("Connections").GetProperty("PostgreSql").GetRawText())!;

                using var jsonDocForNewPostgreSql = JsonDocument.Parse(await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync(oldConnection.Key));
                var newPostgreSqlConnectionOptions = JsonSerializer.Deserialize<PostgreSqlConnectionOptions>(
                    jsonDocForNewPostgreSql.RootElement.GetProperty("Connections").GetProperty("PostgreSql").GetRawText())!;
                
                Assert.NotEqual(oldPostgreSqlConnectionOptions.Username, newPostgreSqlConnectionOptions.Username);
                Assert.DoesNotContain(oldConnectionCredentials.PostgreSqlUsers, u => u.Username == newPostgreSqlConnectionOptions.Username);

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

                // RabbitMQ
                var oldRabbitMqConnectionOptions = JsonSerializer.Deserialize<RabbitMqConnectionOptions>(
                    jsonDocForOldConnections.RootElement.GetProperty("Connections").GetProperty("RabbitMq").GetRawText())!;

                using var jsonDocForNewRabbitMq = JsonDocument.Parse(await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync(oldConnection.Key));
                var newRabbitMqConnectionOptions = JsonSerializer.Deserialize<RabbitMqConnectionOptions>(
                    jsonDocForNewRabbitMq.RootElement.GetProperty("Connections").GetProperty("RabbitMq").GetRawText())!;

                Assert.NotEqual(oldRabbitMqConnectionOptions.Username, newRabbitMqConnectionOptions.Username);
                Assert.DoesNotContain(newRabbitMqConnectionOptions.Username, oldConnectionCredentials.RabbitMqUsers);

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

                // Redis
                var oldRedisConnectionOptions = JsonSerializer.Deserialize<RedisConnectionOptions>(
                    jsonDocForOldConnections.RootElement.GetProperty("Connections").GetProperty("Redis").GetRawText())!;

                using var jsonDocForNewRedis = JsonDocument.Parse(await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync(oldConnection.Key));
                var newRedisConnectionOptions = JsonSerializer.Deserialize<RedisConnectionOptions>(
                    jsonDocForNewRedis.RootElement.GetProperty("Connections").GetProperty("Redis").GetRawText())!;

                Assert.NotEqual(oldRedisConnectionOptions.Username, newRedisConnectionOptions.Username);
                Assert.DoesNotContain(oldConnectionCredentials.RedisUsers, u => u.Username == newRedisConnectionOptions.Username);

                var newRedisUser = await _collectionCluster.AwsFixture.AwsElastiCache.GetUser(newRedisConnectionOptions.Username);
                Assert.NotNull(newRedisUser);
                Assert.Equal(newRedisConnectionOptions.Username, newRedisUser.UserName);
            }

            foreach (var oldCertificate in oldCertificates)
            {
                var newCertificates = Utility.GetOptionsFromJson<CertificatesOptionsBase>(
                    json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync(oldCertificate.Key),
                    elementName: CertificatesOptionsBase.Key)!;

                Assert.NotEqual(oldCertificate.Value.ServerCertificate, newCertificates.ServerCertificate);
                Assert.NotEqual(oldCertificate.Value.ServerPrivateKey, newCertificates.ServerPrivateKey);
                TlsUtility.LoadCertificate(newCertificates.ServerCertificate, newCertificates.ServerPrivateKey); // Should not throw
            }
        }
    }
}
