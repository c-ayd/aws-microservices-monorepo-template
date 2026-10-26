using Shared.AspNetCore.Helpers.Options;

namespace Shared.Aws.Helpers.Options
{
    public class ConnectionsOptionsBase : IOptions
    {
        public static string Key => "Connections";
    }
}
