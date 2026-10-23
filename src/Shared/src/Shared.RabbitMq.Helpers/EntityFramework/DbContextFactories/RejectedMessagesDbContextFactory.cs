using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;

namespace Shared.RabbitMq.Helpers.EntityFramework.DbContextFactories
{
    public class RejectedMessagesDbContextFactory : IDesignTimeDbContextFactory<RejectedMessagesDbContext>
    {
        public RejectedMessagesDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<RejectedMessagesDbContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable("--conn-string"))
                .Options;

            return new RejectedMessagesDbContext(options);
        }
    }
}
