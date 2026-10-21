using ApiGateway.Web.Options;
using Microsoft.Extensions.Options;
using Shared.Redis.Helpers;
using Shared.Redis.Helpers.Extensions;

namespace ApiGateway.Web.DistributedCaches
{
    public class TokenBlacklistRedis : RedisConnection
    {
        public TokenBlacklistRedis(
            IOptions<ConnectionsOptions> connectionsOptions,
            IOptions<CABundleOptions> caBundleOptions)
            : base(
            connectionsOptions.Value.TokenBlacklistRedis,
            caBundleOptions.Value.Bundles
                .FirstOrDefault(b => b.Id == caBundleOptions.Value.CurrentId)!
                .Value)
        {
        }

        public async Task<DateTimeOffset?> GetBlacklistTimeAsync(string accountId)
        {
            var result = await GetDatabase().LoadFromStringAsync<long>(accountId);
            if (result.isKeyFound)
                return DateTimeOffset.FromUnixTimeSeconds(result.value);

            return null;
        }
    }
}
