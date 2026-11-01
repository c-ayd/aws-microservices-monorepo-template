#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using AuthService.Domain.SeedWork;

namespace AuthService.Domain.Entities
{
    public class Account : EntityBase<Guid>, ISoftDelete, IUpdateable
    {
        public string Email { get; set; }
        public string? NewEmail { get; set; }
        public string? PasswordHashed { get; set; }

        public string? PreferredLanguage { get; set; }

        public bool IsEmailVerified { get; set; }
        public bool IsBanned { get; set; }
        public bool IsLocked { get; set; }
        public int FailedLoginAttempts { get; set; }
        public DateTimeOffset? UnlockDate { get; set; }

        public DateTimeOffset? UpdatedDate { get; set; }
        public bool IsDeleted { get; set; }
        public DateTimeOffset? DeletedDate { get; set; }

        // Relationships
        public List<Role> Roles { get; set; } = new List<Role>();
        public List<Session> Sessions { get; set; } = new List<Session>();
        public List<Token> Tokens { get; set; } = new List<Token>();

        // Reserved for EF Core
        private Account() : base()
        {
        }

        public Account(
            string email,
            string passwordHashed,
            string preferredLanguage)
            : base(Guid.CreateVersion7())
        {
            Email = email;
            PasswordHashed = passwordHashed;
            PreferredLanguage = preferredLanguage;
        }

        public void SoftDelete()
        {
            NewEmail = null;
            PasswordHashed = null;
            PreferredLanguage = null;
            IsBanned = false;
            IsLocked = false;
            FailedLoginAttempts = 0;
            UnlockDate = null;
            UpdatedDate = null;
        }
    }
}
