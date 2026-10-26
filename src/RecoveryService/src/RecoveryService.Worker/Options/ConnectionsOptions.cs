using Shared.Aws.Helpers.Options;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Options.Extensions;

namespace RecoveryService.Worker.Options
{
    public class ConnectionsOptions : ConnectionsOptionsBase
    {
        public required PostgreSqlConnectionOptions DroppedMessagesDb { get; set; }
        public required RabbitMqConnectionOptions RabbitMq { get; set; }
    }
}
