using Shared.AspNetCore.Helpers.Options;

namespace ApiGateway.Web.Options
{
    public class CABundleOptions : IOptions
    {
        public static string Key => "CABundle";

        public required string CurrentId { get; set; }
        public required List<BundleInfo> Bundles { get; set; }

        public class BundleInfo
        {
            public required string Id { get; set; }
            public required string Value { get; set; }
        }
    }
}
