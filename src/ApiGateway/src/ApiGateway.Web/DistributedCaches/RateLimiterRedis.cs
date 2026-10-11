using ApiGateway.Web.Exceptions;
using ApiGateway.Web.Options;
using Microsoft.Extensions.Options;
using Shared.Redis;
using StackExchange.Redis;

namespace ApiGateway.Web.DistributedCaches
{
    public class RateLimiterRedis : RedisConnection
    {
        private ILogger<RateLimiterRedis> _logger;
    
        public RateLimiterRedis(
            IOptions<ConnectionsOptions> connectionsOptions,
            ILogger<RateLimiterRedis> logger)
            : base(connectionsOptions.Value.RateLimiterRedis)
        {
            _logger = logger;
        }

        public async Task<(bool isAllowed, long cooldownTimeInSeconds)> CheckRateLimitAsync(string key, int limit, TimeSpan window)
        {
            RedisResult? response;
            try
            {
                response = await GetDatabase().ScriptEvaluateAsync(
                    _luaScript,
                    [
                        (RedisKey)key
                    ],
                    [
                        limit,
                        window.TotalSeconds
                    ]
                );

                if (response == null)
                    throw new RedisNullResponseException();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong. Message: {Message}",
                    exception.Message);

                return (true, (long)window.TotalSeconds);
            }
            
            var results = (RedisValue[])response!;
            return (results[0] == 1, (long)results[1]);
        }

        private const string _luaScript = @"
            local key = KEYS[1]
            local limit = tonumber(ARGV[1])
            local window_seconds = tonumber(ARGV[2])

            local current = redis.call('GET', key)
            if current == false then
                redis.call('SET', key, 1)
                redis.call('EXPIRE', key, window_seconds)
                return {1, window_seconds}
            else
                current = tonumber(current)
                local ttl = redis.call('TTL', key)

                if current < limit then
                    redis.call('INCR', key)
                    return {1, ttl}
                else
                    return {0, ttl}
                end
            end
            ";
    }
}
