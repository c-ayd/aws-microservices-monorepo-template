using Microsoft.EntityFrameworkCore;
using Shared.RabbitMq.Helpers.EntityFramework.Entities;

namespace Shared.RabbitMq.Helpers.EntityFramework.DbContexts
{
    public class DroppedMessagesDbContext : DbContext
    {
        public DbSet<DroppedMessage> DroppedMessages { get; set; }
        public DbSet<WrappedDataKey> WrappedDataKeys { get; set; }

        public DroppedMessagesDbContext(DbContextOptions<DroppedMessagesDbContext> options) : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<DroppedMessage>()
                .HasOne(rm => rm.WrappedDataKey)
                .WithMany(wdk => wdk.DroppedMessages)
                .HasForeignKey(rm => rm.WrappedDataKeyId);
        }
    }
}
