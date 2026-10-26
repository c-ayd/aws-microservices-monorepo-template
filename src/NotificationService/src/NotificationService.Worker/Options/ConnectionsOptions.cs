using Shared.Aws.Helpers.Options;
using Shared.RabbitMq.Helpers.Options.Extensions;

namespace NotificationService.Worker.Options
{
    public class ConnectionsOptions : ConnectionsOptionsBase
    {
        public required RabbitMqConnectionOptions RabbitMq { get; set; }
    }
}
