#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Shared.RabbitMq.Helpers.EntityFramework.Entities
{
    public class WrappedDataKey
    {
        public Guid Id { get; set; }
        public DateTimeOffset CreatedDate { get; set; }

        public byte[] Value { get; set; }

        // Relationships
        public ICollection<DroppedMessage> DroppedMessages { get; set; } = new List<DroppedMessage>();

        // Reserved for EF Core
        private WrappedDataKey()
        {
        }

        public WrappedDataKey(byte[] value)
        {
            Id = Guid.CreateVersion7();
            CreatedDate = DateTimeOffset.UtcNow;
            
            Value = value;
        }
    }
}
