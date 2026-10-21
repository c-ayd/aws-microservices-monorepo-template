using AuthService.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthService.Persistence.DbContextFactories
{
    public class AuthRejectedMessagesDbContextFactory : IDesignTimeDbContextFactory<AuthRejectedMessagesDbContext>
    {
        public AuthRejectedMessagesDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AuthRejectedMessagesDbContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable("--conn-string"))
                .Options;

            return new AuthRejectedMessagesDbContext(options);
        }
    }
}
