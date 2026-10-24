using Initializer.Databases.Options;
using Microsoft.EntityFrameworkCore;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;

namespace Initializer.Databases.Rollouts
{
    public static class RejectedMessagesDb
    {
        public static async Task RolloutAsync(PostgreSqlConnectionOptions connectionOptions, CABundleOptions cABundleOptions)
        {
            using var rejectedMessagesDbContext = Utility.CreateDbContext<RejectedMessagesDbContext>(connectionOptions, cABundleOptions.Value);
            await rejectedMessagesDbContext.Database.MigrateAsync();
        }
    }
}
