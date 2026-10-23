using Initializer.Databases.Rollouts;
using Shared.AspNetCore.Helpers.Options;

namespace Initializer.Databases.Options
{
    public class SeedDataOptions : IOptions
    {
        public static string Key => "SeedData";

        public AuthDb.SeedDataOptions? AuthDbSeedData { get; set; }
    }
}
