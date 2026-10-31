using Common.AspNetCore.Helpers.Options;

namespace Common.Aws.Helpers.Options
{
    public class ConnectionsOptionsBase : IOptions
    {
        public static string Key => "Connections";
    }
}
