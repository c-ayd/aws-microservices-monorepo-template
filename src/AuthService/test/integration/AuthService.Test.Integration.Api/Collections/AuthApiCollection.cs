using System.Text.Json;
using AuthService.Application.Options;
using AuthService.Persistence.DbContexts;
using Microsoft.Extensions.Configuration;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;
using Shared.Test.Helpers;
using Shared.Test.Helpers.Fixtures;

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

        public const string AwsKmsAliasForDroppedMessages = "alias/dropped-messages";
        public const string AwsS3BucketNameForDroppedMessages = "template-dropped-messages";

        public const string AuthDbName = "auth-db";
        public const string DroppedMessagesDbName = "dropped-messages-db";

        public static int AwsPort { get; private set; }
        public static int AuthServicePort { get; private set; }

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

            AwsPort = AwsFixture.GetPort();
            AuthServicePort = KestrelTestHelper.GetAvailablePortNumber();

            AuthApiWebApp = new AuthApiWebAppFactory(
                new JwtKeysOptions()
                {
                    CurrentKeyId = "v2",
                    Keys = new List<JwtKeysOptions.KeyInfo>()
                    {
                        new JwtKeysOptions.KeyInfo()
                        {
                            KeyId = "v1",
                            ExpirationTime = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                            NotBeforeTime = DateTimeOffset.UtcNow.AddMonths(-3).ToUnixTimeSeconds(),
                            PrivateKey = File.ReadAllText("./test-jwt-v1-private.pem"),
                            PublicKey = File.ReadAllText("./test-jwt-v1-public.pem")
                        },
                        new JwtKeysOptions.KeyInfo()
                        {
                            KeyId = "v2",
                            ExpirationTime = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds(),
                            NotBeforeTime = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                            PrivateKey = File.ReadAllText("./test-jwt-v2-private.pem"),
                            PublicKey = File.ReadAllText("./test-jwt-v2-public.pem")
                        }
                    }
                });

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
            private readonly JwtKeysOptions _jwtKeysOptions;

            public AuthApiWebAppFactory(JwtKeysOptions jwtKeysOptions)
                : base(defaultPortConfigName: "HttpsOnly", CABundlePath)
            {
                _jwtKeysOptions = jwtKeysOptions;
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpsPort",
                        AuthServicePort.ToString()),

                    new KeyValuePair<string, string?>($"{JwtOptions.Key}:{nameof(JwtOptions.Issuer)}",
                        $"https://localhost:{AuthServicePort}"),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{AwsPort}"),

                    new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.CurrentKeyId)}",
                        _jwtKeysOptions.CurrentKeyId),
                    .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.KeyId)}",
                        k.KeyId)),
                    .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.ExpirationTime)}",
                        k.ExpirationTime.ToString())),
                    .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.NotBeforeTime)}",
                        k.NotBeforeTime.ToString())),
                    .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.PrivateKey)}",
                        k.PrivateKey)),
                    .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.PublicKey)}",
                        k.PublicKey))
                ]);
            }
        }

        private async Task SetupAwsAsync()
        {
            await AwsFixture.KMS_CreateKekAsync(AwsKmsAliasForDroppedMessages);
            await AwsFixture.S3Bucket_CreateBucketAsync(AwsS3BucketNameForDroppedMessages);

            await AwsFixture.SecretsManager_AddStringAsync("template/auth-svc/connections", JsonSerializer.Serialize(
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
            await AwsFixture.SecretsManager_AddStringAsync("template/auth-svc/certificates", JsonSerializer.Serialize(
                new
                {
                    Certificate = new
                    {
                        ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _authServiceServerCertificateFilePath)),
                        ServerPrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _authServiceServerKeyFilePath))
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
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
