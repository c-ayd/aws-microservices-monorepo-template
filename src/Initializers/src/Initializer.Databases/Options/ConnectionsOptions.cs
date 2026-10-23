using Shared.AspNetCore.Helpers.Options;
using Shared.PostgreSql.Helpers.Options.Extensions;

namespace Initializer.Db.Options
{
    public class ConnectionsOptions : IOptions
    {
        public static string Key => "Connections";

        public required PostgreSqlConnectionOptions AuthDb { get; set; }
    }
}
