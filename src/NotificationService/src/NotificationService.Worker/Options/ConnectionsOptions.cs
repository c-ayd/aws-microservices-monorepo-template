using Shared.AspNetCore.Helpers.Options;
using Shared.RabbitMq.Helpers.Options.Extensions;

namespace NotificationService.Worker.Options
{
    public class ConnectionsOptions : IOptions
    {
        public static string Key => "Connections";

        public required RabbitMqConnectionOptions RabbitMq { get; set; }
    }
}
