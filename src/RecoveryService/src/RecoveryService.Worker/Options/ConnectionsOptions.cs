using Shared.AspNetCore.Helpers.Options;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Options.Extensions;

namespace RecoveryService.Worker.Options
{
    public class ConnectionsOptions : IOptions
    {
        public static string Key => "Connections";

        public required PostgreSqlConnectionOptions RejectedMessagesDb { get; set; }
        public required RabbitMqConnectionOptions RabbitMq { get; set; }
    }
}
