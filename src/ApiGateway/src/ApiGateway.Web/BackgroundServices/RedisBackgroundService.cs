using ApiGateway.Web.DistributedCaches;
using Common.Redis.Helpers;
using Common.Redis.Helpers.BackgroundServices;

namespace ApiGateway.Web.BackgroundServices
{
    public class RedisBackgroundServices : RedisConnectionBackgroundService
    {
        public RedisBackgroundServices(
            TokenBlacklistRedis tokenBlacklist,
            RateLimiterRedis rateLimiterRedis,
            ILogger<RedisBackgroundServices> logger)
            : base(
            redisConnections: new List<RedisConnection>()
            {
                tokenBlacklist,
                rateLimiterRedis
            },
            logger)
        {
        }
    }
}
