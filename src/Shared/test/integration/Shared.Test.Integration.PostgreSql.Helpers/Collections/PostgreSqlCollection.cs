using Shared.Test.Helpers.Fixtures;
using static Shared.Test.Integration.PostgreSql.Helpers.EntityFramework.Extensions.ConfigureDbContextTest;

namespace Shared.Test.Integration.PostgreSql.Helpers.Collections
{
    [CollectionDefinition(nameof(PostgreSqlCollection))]
    public class PostgreSqlCollection : ICollectionFixture<PostgreSqlCollectionCluster>
    {
    }

    public class PostgreSqlCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public PostgreSqlFixture PostgreSqlFixture { get; private set; }

        public PostgreSqlCollectionCluster()
        {
            PostgreSqlFixture = new PostgreSqlFixture();
        }

        public async Task InitializeAsync()
        {
            await PostgreSqlFixture.InitializeAsync(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                certificateKeyFilePath: "Certs/PostgreSql/test-postgresql-server.key",
                certificateFilePath: "Certs/PostgreSql/test-postgresql-server.crt",
                new Dictionary<string, Type>()
                {
                    { "test-db-1", typeof(TestDbContext1) },
                    { "test-db-2", typeof(TestDbContext2) }
                },
                runMigrations: false);
        }

        public async Task DisposeAsync()
        {
            await PostgreSqlFixture.DisposeAsync();
        }
    }
}
