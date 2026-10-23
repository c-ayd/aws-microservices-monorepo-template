using Microsoft.EntityFrameworkCore;
using Shared.RabbitMq.Helpers.EntityFramework.Entities;

namespace AuthService.Persistence.DbContexts
{
    public class AuthRejectedMessagesDbContext : DbContext
    {
        public DbSet<RejectedMessage> RejectedMessages { get; set; }
        public DbSet<WrappedDataKey> WrappedDataKeys { get; set; }

        public AuthRejectedMessagesDbContext(DbContextOptions<AuthRejectedMessagesDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<RejectedMessage>()
                .HasOne(rm => rm.WrappedDataKey)
                .WithMany(wdk => wdk.RejectedMessages)
                .HasForeignKey(rm => rm.WrappedDataKeyId);
        }
    }
}
