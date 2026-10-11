using AuthService.Persistence.DbContexts;
using Shared.Test.Helpers.Fixtures;

namespace AuthDb.Test.Integration.Migrations.Collections
{
    [CollectionDefinition(nameof(PostgreSqlCollection))]
    public class PostgreSqlCollection : ICollectionFixture<PostgreSqlCollectionCluster>
    {
    }

    public class PostgreSqlCollectionCluster : IAsyncLifetime
    {
        public PostgreSqlFixture PostgreSqlFixture { get; private set; }

        public PostgreSqlCollectionCluster()
        {
            PostgreSqlFixture = new PostgreSqlFixture();
        }

        public async Task InitializeAsync()
        {
            await PostgreSqlFixture.InitializeAsync(
                sslEnabled: false,
                dbContexts: new Dictionary<string, Type>()
                {
                    { "auth-db", typeof(AuthDbContext) }
                },
                runMigrations: false);
        }

        public async Task DisposeAsync()
        {
            await PostgreSqlFixture.DisposeAsync();
        }
    }
}
