using Initializer.Databases.Options;
using Microsoft.EntityFrameworkCore;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;

namespace Initializer.Databases.Rollouts
{
    public static class DroppedMessagesDb
    {
        public static async Task RolloutAsync(PostgreSqlConnectionOptions connectionOptions, CABundleOptions cABundleOptions)
        {
            await using var droppedMessagesDbContext = Utility.CreateDbContext<DroppedMessagesDbContext>(connectionOptions, cABundleOptions.Value);
            await droppedMessagesDbContext.Database.MigrateAsync();
        }
    }
}
