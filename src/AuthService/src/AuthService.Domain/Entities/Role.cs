#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using AuthService.Domain.SeedWork;

namespace AuthService.Domain.Entities
{
    public class Role : EntityBase<Guid>
    {
        public string Name { get; set; }

        // Relationships
        public List<Account> Accounts { get; set; } = new List<Account>();

        // Reserved for EF Core
        private Role() : base()
        {
        }

        public Role(
            string name)
            : base(Guid.CreateVersion7())
        {
            Name = name;
        }

        public void SoftDelete()
        {
            // Nothing to set for this entity
        }
    }
}
