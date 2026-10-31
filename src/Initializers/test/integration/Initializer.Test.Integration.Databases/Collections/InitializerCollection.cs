using System.Text.Json;
using AuthService.Persistence.DbContexts;
using Common.RabbitMq.Helpers.EntityFramework.DbContexts;
using Common.Test.Helpers.Fixtures;

namespace Initializer.Test.Integration.Databases.Collections
{
    [CollectionDefinition(nameof(InitializerCollection))]
    public class InitializerCollection : ICollectionFixture<InitializerCollectionCluster>
    {
    }

    public class InitializerCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public const string AuthDbName = "auth-db";
        public const string DroppedMessagesDbName = "dropped-messages-db";

        public AwsFixture AwsFixture { get; private set; }
        public PostgreSqlFixture PostgreSqlFixture { get; private set; }

        public InitializerCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            PostgreSqlFixture = new PostgreSqlFixture();
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
                    dbContexts: new Dictionary<string, Type>()
                    {
                        { AuthDbName, typeof(AuthDbContext) },
                        { DroppedMessagesDbName, typeof(DroppedMessagesDbContext) }
                    },
                    runMigrations: false)
            );

            await SetupAwsAsync();
        }
        
        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                AwsFixture.DisposeAsync(),
                PostgreSqlFixture.DisposeAsync()
            );
        }

        private async Task SetupAwsAsync()
        {
            await AwsFixture.SecretsManager_AddStringAsync("template/authdb-job/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        AuthDb = PostgreSqlFixture.GetConnectionOptions(AuthDbName),
                        DroppedMessagesDb = PostgreSqlFixture.GetConnectionOptions(DroppedMessagesDbName)
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/authdb-job/seed-data", JsonSerializer.Serialize(
                new
                {
                    SeedData = new
                    {
                        AuthDbSeedData = new
                        {
                            Roles = new[]
                            {
                                "Admin"
                            },
                            Accounts = new[]
                            {
                                new { Email = "test@test.com", Role = "Admin" }
                            }
                        }
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
