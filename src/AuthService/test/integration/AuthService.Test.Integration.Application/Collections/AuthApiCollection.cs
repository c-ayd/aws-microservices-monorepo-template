using System.Text.Json;
using AuthService.Application.Options;
using AuthService.Persistence.DbContexts;
using Microsoft.Extensions.Configuration;
using Shared.Test.Helpers;
using Shared.Test.Helpers.Fixtures;

namespace AuthService.Test.Integration.Application.Collections
{
    [CollectionDefinition(nameof(AuthApiCollection))]
    public class AuthApiCollection : ICollectionFixture<AuthApiCollectionCluster>
    {
    }

    public class AuthApiCollectionCluster : IAsyncLifetime
    {
        private const string _caBundlePath = "Certs/test-ca-bundle.crt";
        private const string _authServiceServerKeyFilePath = "Certs/test-auth-service-server.key";
        private const string _authServiceServerCertificateFilePath = "Certs/test-auth-service-server.crt";

        private const string _authDbName = "auth-db";
        private const string _authRejectedMessagesDbName = "auth-rejected-messages-db";

        private static int _awsPort;
        private static int _authServicePort;

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
                    caBundlePath: _caBundlePath,
                    certificateKeyFilePath: "Certs/PostgreSql/test-postgresql-server.key",
                    certificateFilePath: "Certs/PostgreSql/test-postgresql-server.crt",
                    new Dictionary<string, Type>()
                    {
                        { _authDbName, typeof(AuthDbContext) },
                        { _authRejectedMessagesDbName, typeof(AuthRejectedMessagesDbContext) }
                    }),
                RabbitMqFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: _caBundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq"),
                TokenBlacklistRedisFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: _caBundlePath,
                    redisCertificateFolder: "Certs/Redis")
            );

            await AwsFixture.KMS_CreateKekAsync("alias/auth-svc");

            await AwsFixture.SecretsManager_AddStringAsync("template/auth-svc/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        AuthDb = PostgreSqlFixture.GetConnectionOptions(_authDbName),
                        AuthRejectedMessagesDb = PostgreSqlFixture.GetConnectionOptions(_authRejectedMessagesDbName),
                        RabbitMq = RabbitMqFixture.GetConnectionOptions(),
                        TokenBlacklistRedis = TokenBlacklistRedisFixture.GetConnectionOptions()
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/auth-svc/certificates", JsonSerializer.Serialize(
                new
                {
                    Certificate = new
                    {
                        ServerKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _authServiceServerKeyFilePath)),
                        ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _authServiceServerCertificateFilePath))
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/auth-svc/encryption", JsonSerializer.Serialize(
                new
                {
                    Encryption = new
                    {
                        AwsKmsAlias = "alias/auth-svc"
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
                new
                {
                    CABundle = new
                    {
                        Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _caBundlePath))
                    }
                }));

            _awsPort = AwsFixture.GetPort();
            _authServicePort = KestrelHelper.GetFreePort();

            AuthApiWebApp = new AuthApiWebAppFactory(
                new JwtKeysOptions()
                {
                    CurrentKeyId = "k2",
                    Keys = new List<JwtKeysOptions.KeyInfo>()
                    {
                        new JwtKeysOptions.KeyInfo()
                        {
                            KeyId = "k1",
                            PrivateKey = File.ReadAllText("./test-jwt-k1-private.pem"),
                            PublicKey = File.ReadAllText("./test-jwt-k1-public.pem")
                        },
                        new JwtKeysOptions.KeyInfo()
                        {
                            KeyId = "k2",
                            PrivateKey = File.ReadAllText("./test-jwt-k2-private.pem"),
                            PublicKey = File.ReadAllText("./test-jwt-k2-public.pem")
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

            public AuthApiWebAppFactory(JwtKeysOptions jwtKeysOptions) : base(_caBundlePath)
            {
                _jwtKeysOptions = jwtKeysOptions;
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpsPort",
                        _authServicePort.ToString()),

                    new KeyValuePair<string, string?>($"{JwtOptions.Key}:{nameof(JwtOptions.Issuer)}",
                        $"https://localhost:{_authServicePort}"),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{_awsPort}"),

                    new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.CurrentKeyId)}",
                        _jwtKeysOptions.CurrentKeyId),
                    .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.KeyId)}",
                        k.KeyId)),
                    .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.PrivateKey)}",
                        k.PrivateKey)),
                    .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.PublicKey)}",
                        k.PublicKey))
                ]);
            }
        }
    }
}
