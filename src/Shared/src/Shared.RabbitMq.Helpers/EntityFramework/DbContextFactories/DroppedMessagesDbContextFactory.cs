using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;

namespace Shared.RabbitMq.Helpers.EntityFramework.DbContextFactories
{
    public class DroppedMessagesDbContextFactory : IDesignTimeDbContextFactory<DroppedMessagesDbContext>
    {
        public DroppedMessagesDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<DroppedMessagesDbContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable("--conn-string"))
                .Options;

            return new DroppedMessagesDbContext(options);
        }
    }
}
