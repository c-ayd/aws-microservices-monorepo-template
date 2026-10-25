using System.Text.Json;
using AuthService.Persistence.DbContexts;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;
using Shared.Test.Helpers.Fixtures;

namespace Initializer.Test.Integration.Databases.Collections
{
    [CollectionDefinition(nameof(InitializerCollection))]
    public class InitializerCollection : ICollectionFixture<InitializerCollectionCluster>
    {
    }

    public class InitializerCollectionCluster : IAsyncLifetime
    {
        private const string _caBundlePath = "Certs/test-ca-bundle.crt";

        private const string _authDbName = "auth-db";
        private const string _droppedMessagesDbName = "dropped-messages-db";

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
                    caBundlePath: _caBundlePath,
                    certificateKeyFilePath: "Certs/PostgreSql/test-postgresql-server.key",
                    certificateFilePath: "Certs/PostgreSql/test-postgresql-server.crt",
                    dbContexts: new Dictionary<string, Type>()
                    {
                        { _authDbName, typeof(AuthDbContext) },
                        { _droppedMessagesDbName, typeof(DroppedMessagesDbContext) }
                    },
                    runMigrations: false)
            );

            await AwsFixture.SecretsManager_AddStringAsync("template/authdb-job/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        AuthDb = PostgreSqlFixture.GetConnectionOptions(_authDbName),
                        DroppedMessagesDb = PostgreSqlFixture.GetConnectionOptions(_droppedMessagesDbName)
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
                        Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _caBundlePath))
                    }
                }));
        }
        
        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                AwsFixture.DisposeAsync(),
                PostgreSqlFixture.DisposeAsync()
            );
        }
    }
}
