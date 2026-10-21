using Shared.AspNetCore.Helpers.Options;

namespace Shared.Aws.Helpers.Options
{
    public abstract class CABundleOptionsBase : IOptions
    {
        public static string Key => "CABundle";

        public required string Value { get; set; }
    }
}
