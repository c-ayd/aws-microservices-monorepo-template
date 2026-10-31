using Common.AspNetCore.Helpers.Options;

namespace Common.Aws.Helpers.Options
{
    public abstract class CABundleOptionsBase : IOptions
    {
        public static string Key => "CABundle";

        public required string Value { get; set; }
    }
}
