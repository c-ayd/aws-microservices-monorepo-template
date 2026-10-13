using Shared.AspNetCore.Helpers.Options;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.Redis.Helpers.Options.Extensions;

namespace AuthService.Application.Options
{
    public class ConnectionsOptions : IOptions
    {
        public static string Key => "Connections";

        public required PostgreSqlConnectionOptions AuthDb { get; set; }
        public required PostgreSqlConnectionOptions AuthRejectedMessagesDb { get; set; }
        public required RabbitMqConnectionOptions RabbitMq { get; set; }
        public required RedisConnectionOptions TokenBlacklistRedis { get; set; }
    }
}
