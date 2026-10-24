#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace Shared.RabbitMq.Helpers.EntityFramework.Entities
{
    public class WrappedDataKey
    {
        public Guid Id { get; set; }
        public byte[] Value { get; set; }

        // Relationships
        public ICollection<RejectedMessage> RejectedMessages { get; set; } = new List<RejectedMessage>();

        // Reserved for EF Core
        private WrappedDataKey()
        {
        }

        public WrappedDataKey(byte[] value)
        {
            Id = Guid.CreateVersion7();
            
            Value = value;
        }
    }
}
