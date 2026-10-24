using Shared.AspNetCore.Helpers.Options;
using Shared.PostgreSql.Helpers.Options.Extensions;

namespace Initializer.Databases.Options
{
    public class ConnectionsOptions : IOptions
    {
        public static string Key => "Connections";

        public required PostgreSqlConnectionOptions AuthDb { get; set; }
        public required PostgreSqlConnectionOptions RejectedMessagesDb { get; set; }
    }
}
