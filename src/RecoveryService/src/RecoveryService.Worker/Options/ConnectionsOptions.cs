using Common.Aws.Helpers.Options;
using Common.PostgreSql.Helpers.Options.Extensions;
using Common.RabbitMq.Helpers.Options.Extensions;

namespace RecoveryService.Worker.Options
{
    public class ConnectionsOptions : ConnectionsOptionsBase
    {
        public required PostgreSqlConnectionOptions DroppedMessagesDb { get; set; }
        public required RabbitMqConnectionOptions RabbitMq { get; set; }
    }
}
