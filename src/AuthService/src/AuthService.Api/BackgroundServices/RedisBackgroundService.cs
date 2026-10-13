using AuthService.Application.Abstractions.DistributedCaches;
using Shared.Redis.Helpers;
using Shared.Redis.Helpers.BackgroundServices;

namespace AuthService.Api.BackgroundServices
{
    public class RedisBackgroundServices : RedisConnectionBackgroundService
    {
        public RedisBackgroundServices(
            ITokenBlacklistRedis tokenBlacklist,
            ILogger<RedisBackgroundServices> logger)
            : base(
            redisConnections: new List<RedisConnection>()
            {
                (RedisConnection)tokenBlacklist
            },
            logger)
        {
        }
    }
}
