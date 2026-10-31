using Initializer.Databases.Options;
using Microsoft.EntityFrameworkCore;
using Common.PostgreSql.Helpers.Options.Extensions;
using Common.RabbitMq.Helpers.EntityFramework.DbContexts;

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
