using AuthService.Application.Abstractions.DistributedCaches;
using AuthService.Application.Options;
using Microsoft.Extensions.Options;
using Shared.Redis.Helpers;
using Shared.Redis.Helpers.Extensions;

namespace AuthService.Persistence.DistributedCaches
{
    public class TokenBlacklistRedis : RedisConnection, ITokenBlacklistRedis
    {
        public TokenBlacklistRedis(
            IOptions<ConnectionsOptions> connectionsOptions,
            IOptions<CABundleOptions> caBundleOptions)
            : base(
            connectionsOptions.Value.TokenBlacklistRedis,
            caBundleOptions.Value.Value)
        {
        }

        public async Task AddAsync(string accountId, TimeSpan accessTokenLifespan)
        {
            await GetDatabase().SaveAsStringAsync(accountId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), accessTokenLifespan);
        }

        public async Task DeleteAsync(string accountId)
        {
            await GetDatabase().KeyDeleteAsync(accountId);
        }
    }
}
