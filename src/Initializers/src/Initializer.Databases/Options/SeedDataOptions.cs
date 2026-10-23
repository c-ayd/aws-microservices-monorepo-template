using Shared.AspNetCore.Helpers.Options;

namespace Initializer.Db.Options
{
    public class SeedDataOptions : IOptions
    {
        public static string Key => "SeedData";

        public required List<string> Roles { get; set; }
        public required List<AccountDetails> Accounts { get; set; }

        public class AccountDetails
        {
            public required string Email { get; set; }
            public string? Role { get; set; }
        }
    }
}
