using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Text.Json;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.Aws.Helpers.Options;
using Common.Test.Helpers.Structures;
using Lambda.DeleteOldConnections;
using Lambda.DeleteOldConnections.Utilities;
using Lambdas.Test.Integration.SecretsRotations.Collections;
using Npgsql;
using RabbitMQ.Client;

namespace Lambda.Test.Integration.DeleteOldConnections
{
    [Collection(nameof(LambdaCollection))]
    public class FunctionTest
    {
        private LambdaCollectionCluster _collectionCluster;

        private readonly Function _lambdaFunction;

        public FunctionTest(LambdaCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;

            _lambdaFunction = new Function();
        }

        [Fact]
        public async Task FunctionHandler_WhenIsCalled_ShouldDeleteOldUsers()
        {
            // Arrange
            Utility.TryDeserializeJson<CABundleOptionsBase>(
                json: await _collectionCluster.AwsFixture.AwsSecretsManager.GetStringAsync("template/cert/ca-bundle"),
                section: CABundleOptionsBase.Key,
                out var cABundleOptions);
            var caBundle = TlsUtility.LoadCertificateChain(cABundleOptions!.Value);
            
            var input = JsonSerializer.Serialize(new
            {
                detail = new EventJson()
                {
                    CABundleSecretId = "template/cert/ca-bundle",
                    AdminConnectionsSecretId = "template/admin-connections",
                    OldUsersSecretId = "template/old-users",
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

            var postgreSqlConnectionOptions = _collectionCluster.PostgreSqlFixture.GetConnectionOptions(LambdaCollectionCluster.PostgreSqlDbName);
            var rabbitMqConnectionOptions = _collectionCluster.RabbitMqFixture.GetConnectionOptions();
            foreach (var oldUsername in LambdaCollectionCluster.OldUsernames)
            {
                // PostgreSQL
                var connStringBuilder = new NpgsqlConnectionStringBuilder()
                {
                    Host = postgreSqlConnectionOptions.Host,
                    Port = postgreSqlConnectionOptions.Port,
                    Database = postgreSqlConnectionOptions.Database,
                    Username = oldUsername,
                    Password = LambdaCollectionCluster.DefaultPassword,
                    SslMode = SslMode.Require
                };

                var builder = new NpgsqlDataSourceBuilder(connStringBuilder.ConnectionString);
                builder.UseSslClientAuthenticationOptionsCallback(options =>
                {
                    options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                    options.TargetHost = postgreSqlConnectionOptions.Ssl.ServerName;
                    options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                    };
                });

                await using var dataSource = builder.Build();
                var exception = await Record.ExceptionAsync(async () =>
                {
                    await using var postgreSqlConnection = await dataSource.OpenConnectionAsync();
                });
                
                Assert.NotNull(exception);

                // RabbitMQ
                var connectionFactory = new ConnectionFactory()
                {
                    HostName = rabbitMqConnectionOptions.Host,
                    Port = rabbitMqConnectionOptions.Port,
                    UserName = oldUsername,
                    Password = LambdaCollectionCluster.DefaultPassword,
                    AutomaticRecoveryEnabled = false,
                    TopologyRecoveryEnabled = false,
                    Ssl = new SslOption()
                    {
                        Enabled = true,
                        Version = SslProtocols.Tls12 | SslProtocols.Tls13,
                        AcceptablePolicyErrors = SslPolicyErrors.None,
                        ServerName = rabbitMqConnectionOptions.Ssl.ServerName!,
                        CertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                        {
                            return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                        }
                    }
                };

                exception = await Record.ExceptionAsync(async () =>
                {
                    await using var rabbitMqConnection = await connectionFactory.CreateConnectionAsync();
                });

                Assert.NotNull(exception);

                // Redis
                var oldRedisUser = await _collectionCluster.AwsFixture.AwsElastiCache.GetUser(oldUsername);
                Assert.Null(oldRedisUser);
            }
        }
    }
}
