using Shared.AspNetCore.Helpers.Options;

namespace Shared.Aws.Helpers.Options
{
    public class AwsSesOptions : IOptions
    {
        public static string Key => "AwsSes";

        public required string FromEmailAddress { get; set; }
    }
}
