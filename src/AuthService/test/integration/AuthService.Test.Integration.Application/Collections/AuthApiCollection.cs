using AuthService.Application.Options;
using AuthService.Persistence.DbContexts;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Shared.AspNetCore.Helpers.Kestrel.Options.Extensions;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.Redis.Options.Extensions;
using Shared.Test.Helpers.Fixtures;

namespace AuthService.Test.Integration.Application.Collections
{
    [CollectionDefinition(nameof(AuthApiCollection))]
    public class AuthApiCollection : ICollectionFixture<AuthApiCollectionCluster>
    {
    }

    public class AuthApiCollectionCluster :  IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public const string AuthDbName = "auth-db";
        public const string AuthRejectedMessagesDbName = "auth-rejected-messages-db";

        public PostgreSqlFixture PostgreSqlFixture { get; private set; }
        public RabbitMqFixture RabbitMqFixture { get; private set; }
        public RedisFixture TokenBlacklistRedisFixture { get; private set; }

        public AuthApiWebAppFactory AuthApiWebApp { get; private set; } = null!;

        public AuthApiCollectionCluster()
        {
            PostgreSqlFixture = new PostgreSqlFixture();
            RabbitMqFixture = new RabbitMqFixture();
            TokenBlacklistRedisFixture = new RedisFixture();
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                PostgreSqlFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    certificateKeyFilePath: "Certs/PostgreSql/test-postgresql-server.key",
                    certificateFilePath: "Certs/PostgreSql/test-postgresql-server.crt",
                    new Dictionary<string, Type>()
                    {
                        { AuthDbName, typeof(AuthDbContext) },
                        { AuthRejectedMessagesDbName, typeof(AuthRejectedMessagesDbContext) }
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

            AuthApiWebApp = new AuthApiWebAppFactory(
                new ConnectionsOptions()
                {
                    AuthDb = PostgreSqlFixture.GetConnectionOptions(AuthDbName),
                    AuthRejectedMessagesDb = PostgreSqlFixture.GetConnectionOptions(AuthRejectedMessagesDbName),
                    RabbitMq = RabbitMqFixture.GetConnectionOptions(),
                    TokenBlacklistRedis = TokenBlacklistRedisFixture.GetConnectionOptions()
                },
                new JwtKeysOptions()
                {
                    CurrentKeyId = "k2",
                    Keys = new List<JwtKeysOptions.KeyInfo>()
                    {
                        new JwtKeysOptions.KeyInfo()
                        {
                            KeyId = "k1",
                            PrivateKey = File.ReadAllText("./test_jwt_k1_private.txt"),
                            PublicKey = File.ReadAllText("./test_jwt_k1_public.txt")
                        },
                        new JwtKeysOptions.KeyInfo()
                        {
                            KeyId = "k2",
                            PrivateKey = File.ReadAllText("./test_jwt_k2_private.txt"),
                            PublicKey = File.ReadAllText("./test_jwt_k2_public.txt")
                        }
                    }
                });
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                PostgreSqlFixture.DisposeAsync(),
                RabbitMqFixture.DisposeAsync(),
                TokenBlacklistRedisFixture.DisposeAsync()
            );

            await AuthApiWebApp.DisposeAsync();
        }

        public class AuthApiWebAppFactory : WebAppFactoryFixture<Program>
        {
            private readonly ConnectionsOptions _connectionsOptions;
            private readonly JwtKeysOptions _jwtKeysOptions;

            public AuthApiWebAppFactory(
                ConnectionsOptions connectionsOptions,
                JwtKeysOptions jwtKeysOptions)
                : base(CABundlePath)
            {
                _connectionsOptions = connectionsOptions;
                _jwtKeysOptions = jwtKeysOptions;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);

                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection([
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthDb)}:{nameof(PostgreSqlConnectionOptions.Host)}",
                            _connectionsOptions.AuthDb.Host),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthDb)}:{nameof(PostgreSqlConnectionOptions.Port)}",
                            _connectionsOptions.AuthDb.Port.ToString()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthDb)}:{nameof(PostgreSqlConnectionOptions.Database)}",
                            _connectionsOptions.AuthDb.Database),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthDb)}:{nameof(PostgreSqlConnectionOptions.Username)}",
                            _connectionsOptions.AuthDb.Username),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthDb)}:{nameof(PostgreSqlConnectionOptions.Password)}",
                            _connectionsOptions.AuthDb.Password),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthDb)}:{nameof(PostgreSqlConnectionOptions.Ssl)}:{nameof(SslOptions.CABundlePath)}",
                            _connectionsOptions.AuthDb.Ssl!.CABundlePath),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthDb)}:{nameof(PostgreSqlConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                            _connectionsOptions.AuthDb.Ssl!.ServerName),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthDb)}:{nameof(PostgreSqlConnectionOptions.RetryMechanism)}",
                            null),

                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthRejectedMessagesDb)}:{nameof(PostgreSqlConnectionOptions.Host)}",
                            _connectionsOptions.AuthRejectedMessagesDb.Host),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthRejectedMessagesDb)}:{nameof(PostgreSqlConnectionOptions.Port)}",
                            _connectionsOptions.AuthRejectedMessagesDb.Port.ToString()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthRejectedMessagesDb)}:{nameof(PostgreSqlConnectionOptions.Database)}",
                            _connectionsOptions.AuthRejectedMessagesDb.Database),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthRejectedMessagesDb)}:{nameof(PostgreSqlConnectionOptions.Username)}",
                            _connectionsOptions.AuthRejectedMessagesDb.Username),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthRejectedMessagesDb)}:{nameof(PostgreSqlConnectionOptions.Password)}",
                            _connectionsOptions.AuthRejectedMessagesDb.Password),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthRejectedMessagesDb)}:{nameof(PostgreSqlConnectionOptions.Ssl)}:{nameof(SslOptions.CABundlePath)}",
                            _connectionsOptions.AuthRejectedMessagesDb.Ssl!.CABundlePath),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthRejectedMessagesDb)}:{nameof(PostgreSqlConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                            _connectionsOptions.AuthRejectedMessagesDb.Ssl!.ServerName),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.AuthRejectedMessagesDb)}:{nameof(PostgreSqlConnectionOptions.RetryMechanism)}",
                            null),

                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Host)}",
                            _connectionsOptions.RabbitMq.Host),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Port)}",
                            _connectionsOptions.RabbitMq.Port.ToString()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Username)}",
                            _connectionsOptions.RabbitMq.Username),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Password)}",
                            _connectionsOptions.RabbitMq.Password),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Ssl)}:{nameof(SslOptions.CABundlePath)}",
                            _connectionsOptions.RabbitMq.Ssl!.CABundlePath),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                            _connectionsOptions.RabbitMq.Ssl!.ServerName),

                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Host)}",
                            _connectionsOptions.TokenBlacklistRedis.Host),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Port)}",
                            _connectionsOptions.TokenBlacklistRedis.Port.ToString()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Password)}",
                            _connectionsOptions.TokenBlacklistRedis.Password),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.AbortOnConnectionFail)}",
                            _connectionsOptions.TokenBlacklistRedis.AbortOnConnectionFail.ToString().ToLower()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Ssl)}:{nameof(SslOptions.CABundlePath)}",
                            _connectionsOptions.TokenBlacklistRedis.Ssl!.CABundlePath),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                            _connectionsOptions.TokenBlacklistRedis.Ssl!.ServerName),

                        new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.CurrentKeyId)}",
                            _jwtKeysOptions.CurrentKeyId),
                        .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.KeyId)}",
                            k.KeyId)),
                        .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.PrivateKey)}",
                            k.PrivateKey)),
                        .._jwtKeysOptions.Keys.Select((k, i) => new KeyValuePair<string, string?>($"{JwtKeysOptions.Key}:{nameof(JwtKeysOptions.Keys)}:{i}:{nameof(JwtKeysOptions.KeyInfo.PublicKey)}",
                            k.PublicKey))
                    ]);
                });
            }
        }
    }
}
