using Shared.Aws.Helpers.Options;
using Shared.PostgreSql.Helpers.Options.Extensions;

namespace Initializer.Databases.Options
{
    public class ConnectionsOptions : ConnectionsOptionsBase
    {
        public required PostgreSqlConnectionOptions AuthDb { get; set; }
        public required PostgreSqlConnectionOptions DroppedMessagesDb { get; set; }
    }
}
