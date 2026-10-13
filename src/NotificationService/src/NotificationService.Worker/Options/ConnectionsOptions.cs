using Shared.AspNetCore.Helpers;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Options.Extensions;

namespace NotificationService.Worker.Options
{
    public class ConnectionsOptions : IOptions
    {
        public static string Key => "Connections";

        public required RabbitMqConnectionOptions RabbitMq { get; set; }
        public required PostgreSqlConnectionOptions TemplateDb { get; set; }
    }
}
