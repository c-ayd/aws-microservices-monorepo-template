using Common.Aws.Helpers.Options;
using Common.PostgreSql.Helpers.Options.Extensions;

namespace Initializer.Databases.Options
{
    public class ConnectionsOptions : ConnectionsOptionsBase
    {
        public required PostgreSqlConnectionOptions AuthDb { get; set; }
        public required PostgreSqlConnectionOptions DroppedMessagesDb { get; set; }
    }
}
