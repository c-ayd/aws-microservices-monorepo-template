using Shared.AspNetCore.Helpers;
using Shared.Redis.Options.Extensions;

namespace ApiGateway.Web.Options
{
    public class ConnectionsOptions : IOptions
    {
        public static string Key => "Connections";

        public required RedisConnectionOptions TokenBlacklistRedis { get; set; }
        public required RedisConnectionOptions RateLimiterRedis { get; set; }
    }
}
