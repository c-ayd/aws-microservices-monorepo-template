using AuthService.Persistence.DbContexts;
using Common.RabbitMq.Helpers.EntityFramework.DbContexts;
using Common.Test.Helpers.Fixtures;

namespace Initializer.Test.Integration.Databases
{
    public static class Utility
    {
        public const string AuthDbName = "auth-db";
        public const string DroppedMessagesDbName = "dropped-messages-db";

        public static async Task InitializePostgreSqlFixtureAsync(PostgreSqlFixture postgreSqlFixture)
        {
            await postgreSqlFixture.InitializeAsync(
                sslEnabled: true,
                caBundlePath: "Certs/test-ca-bundle.crt",
                serverCertificatePath: "Certs/PostgreSql/test-postgresql-server.crt",
                serverCertificatePrivateKeyPath: "Certs/PostgreSql/test-postgresql-server.key",
                dbContexts: new Dictionary<string, Type>()
                {
                    { AuthDbName, typeof(AuthDbContext) },
                    { DroppedMessagesDbName, typeof(DroppedMessagesDbContext) }
                },
                runMigrations: false);
        }
    }
}
