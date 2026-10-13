using AuthService.Persistence.DbContexts;
using Shared.Test.Helpers.Fixtures;

namespace AuthService.Test.Integration.Persistence.Collections
{
    [CollectionDefinition(nameof(AuthDbContextCollection))]
    public class AuthDbContextCollection : ICollectionFixture<AuthDbContextCollectionCluster>
    {
    }

    public class AuthDbContextCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public const string AuthDbName = "auth-db";

        public PostgreSqlFixture PostgreSqlFixture { get; private set; }

        public AuthDbContextCollectionCluster()
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
                    { AuthDbName, typeof(AuthDbContext) }
                });
        }

        public async Task DisposeAsync()
        {
            await PostgreSqlFixture.DisposeAsync();
        }
    }
}
