using Common.Aws.Helpers.Options;
using Common.PostgreSql.Helpers.Options.Extensions;
using Common.RabbitMq.Helpers.Options.Extensions;
using Common.Redis.Helpers.Options.Extensions;

namespace AuthService.Application.Options
{
    public class ConnectionsOptions : ConnectionsOptionsBase
    {
        public required PostgreSqlConnectionOptions AuthDb { get; set; }
        public required PostgreSqlConnectionOptions DroppedMessagesDb { get; set; }
        public required RabbitMqConnectionOptions RabbitMq { get; set; }
        public required RedisConnectionOptions TokenBlacklistRedis { get; set; }
    }
}
