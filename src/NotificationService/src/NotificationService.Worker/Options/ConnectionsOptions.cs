using Common.Aws.Helpers.Options;
using Common.RabbitMq.Helpers.Options.Extensions;

namespace NotificationService.Worker.Options
{
    public class ConnectionsOptions : ConnectionsOptionsBase
    {
        public required RabbitMqConnectionOptions RabbitMq { get; set; }
    }
}
