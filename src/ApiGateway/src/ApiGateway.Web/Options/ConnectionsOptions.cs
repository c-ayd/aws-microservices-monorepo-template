using Shared.Aws.Helpers.Options;
using Shared.Redis.Helpers.Options.Extensions;

namespace ApiGateway.Web.Options
{
    public class ConnectionsOptions : ConnectionsOptionsBase
    {
        public required RedisConnectionOptions TokenBlacklistRedis { get; set; }
        public required RedisConnectionOptions RateLimiterRedis { get; set; }
    }
}
