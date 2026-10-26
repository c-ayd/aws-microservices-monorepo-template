using AuthService.Persistence.DbContexts;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;
using Shared.Test.Helpers.Fixtures;

namespace Initializer.Test.Integration.Databases
{
    public static class Utility
    {
        public static async Task InitializePostgreSqlFixtureAsync(PostgreSqlFixture postgreSqlFixture)
        {
            await postgreSqlFixture.InitializeAsync(
                sslEnabled: true,
                caBundlePath: "Certs/test-ca-bundle.crt",
                serverCertificatePath: "Certs/PostgreSql/test-postgresql-server.crt",
                serverCertificatePrivateKeyPath: "Certs/PostgreSql/test-postgresql-server.key",
                dbContexts: new Dictionary<string, Type>()
                {
                    { "auth-db", typeof(AuthDbContext) },
                    { "dropped-messages-db", typeof(DroppedMessagesDbContext) }
                },
                runMigrations: false);
        }
    }
}
