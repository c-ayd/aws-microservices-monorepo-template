using System.Text.Json;
using AuthService.Application.Options;
using AuthService.Persistence.DbContexts;
using Microsoft.Extensions.Configuration;
using Common.RabbitMq.Helpers.EntityFramework.DbContexts;
using Common.Test.Helpers;
using Common.Test.Helpers.Fixtures;

namespace AuthService.Test.Integration.Api.Collections
{
    [CollectionDefinition(nameof(AuthApiCollection))]
    public class AuthApiCollection : ICollectionFixture<AuthApiCollectionCluster>
    {
    }

    public class AuthApiCollectionCluster :  IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";
        
        private const string _authServiceServerKeyFilePath = "Certs/test-auth-service-server.key";
        private const string _authServiceServerCertificateFilePath = "Certs/test-auth-service-server.crt";

        private const string _jwtPublicKeyV1Path = "test-jwt-v1-public.pem";
        private const string _jwtPrivateKeyV1Path = "test-jwt-v1-private.pem";
        private const string _jwtPublicKeyV2Path = "test-jwt-v2-public.pem";
        private const string _jwtPrivateKeyV2Path = "test-jwt-v2-private.pem";

        public const string AwsKmsAliasForDroppedMessages = "alias/dropped-messages";
        public const string AwsS3BucketNameForDroppedMessages = "template-dropped-messages";

        public const string AuthDbName = "auth-db";
        public const string DroppedMessagesDbName = "dropped-messages-db";

        public static int AwsPort { get; private set; }
        public static int AuthServiceHttpsPort { get; private set; }
        public static int AuthServiceGrpcsPort { get; private set; }

        public AwsFixture AwsFixture { get; private set; }
        public PostgreSqlFixture PostgreSqlFixture { get; private set; }
        public RabbitMqFixture RabbitMqFixture { get; private set; }
        public RedisFixture TokenBlacklistRedisFixture { get; private set; }

        public AuthApiWebAppFactory AuthApiWebApp { get; private set; } = null!;

        public AuthApiCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            PostgreSqlFixture = new PostgreSqlFixture();
            RabbitMqFixture = new RabbitMqFixture();
            TokenBlacklistRedisFixture = new RedisFixture();
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                AwsFixture.InitializeAsync(),
                PostgreSqlFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    serverCertificatePath: "Certs/PostgreSql/test-postgresql-server.crt",
                    serverCertificatePrivateKeyPath: "Certs/PostgreSql/test-postgresql-server.key",
                    new Dictionary<string, Type>()
                    {
                        { AuthDbName, typeof(AuthDbContext) },
                        { DroppedMessagesDbName, typeof(DroppedMessagesDbContext) }
                    }),
                RabbitMqFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq"),
                TokenBlacklistRedisFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    redisCertificateFolder: "Certs/Redis")
            );

            await SetupAwsAsync();

            AwsPort = AwsFixture.GetMappedPort();
            var availablePorts = KestrelTestHelper.GetMultipleAvailablePortNumbers(numberOfPorts: 2);
            AuthServiceHttpsPort = availablePorts[0];
            AuthServiceGrpcsPort = availablePorts[1];

            AuthApiWebApp = new AuthApiWebAppFactory();
            AuthApiWebApp.StartServer();
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                AwsFixture.DisposeAsync(),
                PostgreSqlFixture.DisposeAsync(),
                RabbitMqFixture.DisposeAsync(),
                TokenBlacklistRedisFixture.DisposeAsync()
            );

            await AuthApiWebApp.DisposeAsync();
        }

        public class AuthApiWebAppFactory : WebAppFactoryFixture<Program>
        {
            public AuthApiWebAppFactory()
                : base(defaultPortConfigName: "HttpsOnly", CABundlePath)
            {
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpsPort",
                        AuthServiceHttpsPort.ToString()),
                    new KeyValuePair<string, string?>("GrpcsPort",
                        AuthServiceGrpcsPort.ToString()),

                    new KeyValuePair<string, string?>($"{JwtOptions.Key}:{nameof(JwtOptions.Issuer)}",
                        $"https://localhost:{AuthServiceHttpsPort}"),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{AwsPort}")
                ]);
            }
        }

        private async Task SetupAwsAsync()
        {
            await AwsFixture.AwsKms.CreateKekAsync(AwsKmsAliasForDroppedMessages);
            await AwsFixture.AwsS3Bucket.CreateBucketAsync(AwsS3BucketNameForDroppedMessages);

            await AwsFixture.AwsSecretsManager.AddStringAsync("template/auth-svc/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        AuthDb = PostgreSqlFixture.GetConnectionOptions(AuthDbName),
                        DroppedMessagesDb = PostgreSqlFixture.GetConnectionOptions(DroppedMessagesDbName),
                        RabbitMq = RabbitMqFixture.GetConnectionOptions(),
                        TokenBlacklistRedis = TokenBlacklistRedisFixture.GetConnectionOptions()
                    }
                }));
            await AwsFixture.AwsSecretsManager.AddStringAsync("template/jwt", JsonSerializer.Serialize(
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
                                PublicKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _jwtPublicKeyV1Path)),
                                PrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _jwtPrivateKeyV1Path)),
                                NotBeforeTime = DateTimeOffset.UtcNow.AddMonths(-3).ToUnixTimeSeconds(),
                                ExpirationTime = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds()
                            },
                            new
                            {
                                KeyId = "v2",
                                PublicKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _jwtPublicKeyV2Path)),
                                PrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _jwtPrivateKeyV2Path)),
                                NotBeforeTime = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                                ExpirationTime = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds(),
                            }
                        }
                    }
                }
            ));
            await AwsFixture.AwsSecretsManager.AddStringAsync("template/auth-svc/certificates", JsonSerializer.Serialize(
                new
                {
                    Certificates = new
                    {
                        ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _authServiceServerCertificateFilePath)),
                        ServerPrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _authServiceServerKeyFilePath))
                    }
                }));
            await AwsFixture.AwsSecretsManager.AddStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
                new
                {
                    CABundle = new
                    {
                        Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, CABundlePath))
                    }
                }));
        }
    }
}
