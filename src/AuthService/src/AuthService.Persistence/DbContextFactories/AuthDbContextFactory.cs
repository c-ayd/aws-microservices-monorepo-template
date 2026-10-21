using AuthService.Persistence.DbContexts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AuthService.Persistence.DbContextFactories
{
    public class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
    {
        public AuthDbContext CreateDbContext(string[] args)
        {
            var options = new DbContextOptionsBuilder<AuthDbContext>()
                .UseNpgsql(Environment.GetEnvironmentVariable("--conn-string"))
                .Options;

            return new AuthDbContext(options);
        }
    }
}
