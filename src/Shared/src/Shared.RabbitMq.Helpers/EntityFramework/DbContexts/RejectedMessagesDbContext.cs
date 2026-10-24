using Microsoft.EntityFrameworkCore;
using Shared.RabbitMq.Helpers.EntityFramework.Entities;

namespace Shared.RabbitMq.Helpers.EntityFramework.DbContexts
{
    public class RejectedMessagesDbContext : DbContext
    {
        public DbSet<RejectedMessage> RejectedMessages { get; set; }
        public DbSet<WrappedDataKey> WrappedDataKeys { get; set; }

        public RejectedMessagesDbContext(DbContextOptions<RejectedMessagesDbContext> options) : base(options)
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
