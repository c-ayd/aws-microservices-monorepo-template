using System.Reflection;
using AuthService.Application.Options;
using AuthService.Persistence.DistributedCaches;
using Microsoft.Extensions.Options;
using Shared.AspNetCore.Helpers.Kestrel.Options.Extensions;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.Redis.Helpers;
using Shared.Test.Helpers.Fixtures;

namespace AuthService.Test.Integration.Persistence.DistributedCaches
{
    public class TokenBlacklistTest : IClassFixture<RedisFixture>, IAsyncLifetime
    {
        private readonly string _caBundlePath = "Certs/test-ca-bundle.crt";
        private readonly RedisFixture _redisFixture;

        private TokenBlacklistRedis? _tokenBlacklist;

        public TokenBlacklistTest(RedisFixture redisFixture)
        {
            _redisFixture = redisFixture;
        }

        public async Task InitializeAsync()
        {
            await _redisFixture.InitializeAsync(
                sslEnabled: true,
                caBundlePath: _caBundlePath,
                redisCertificateFolder: "Certs/Redis");

            var connectionsOptions = new ConnectionsOptions()
            {
                TokenBlacklistRedis = _redisFixture.GetConnectionOptions(),
                AuthDb = _placeholderPostgreSqlConnectionOptions,
                RejectedMessagesDb = _placeholderPostgreSqlConnectionOptions,
                RabbitMq = _placeholderRabbitMqConnectionOptions
            };

            var caBundleOptions = new CABundleOptions()
            {
                Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _caBundlePath))
            };

            _tokenBlacklist = new TokenBlacklistRedis(
                Options.Create(connectionsOptions),
                Options.Create(caBundleOptions));

            var connectAsyncMethod = typeof(RedisConnection).GetMethod("ConnectAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)connectAsyncMethod.Invoke(_tokenBlacklist, [default])!;
        }

        public async Task DisposeAsync()
        {
            await _redisFixture.DisposeAsync();
        }

        [Fact]
        public async Task AddAsync_WhenEntryWithAccountIdDoesNotExist_ShouldCreateNewEntry()
        {
            // Arrange
            var accountId = Guid.NewGuid().ToString();
            var expirationTime = TimeSpan.FromMinutes(5);
            var now = DateTimeOffset.UtcNow;

            // Act
            await _tokenBlacklist!.AddAsync(accountId, expirationTime);

            // Assert
            var db = _redisFixture.GetDatabase();
            var result = await db.StringGetAsync(accountId);
            var remainingTime = await db.KeyTimeToLiveAsync(accountId);

            Assert.True(result.HasValue, "An entry with a given account ID is not found.");
            Assert.InRange((expirationTime - remainingTime!).Value.TotalMinutes, -1, 1);

            var value = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(result.ToString()));
            Assert.InRange((value - now).TotalMinutes, -1, 1);
        }

        [Fact]
        public async Task AddAsync_WhenEntryWithAccountIdExists_ShouldReplaceValueAndResetExpirationTime()
        {
            // Arrange
            var accountId = Guid.NewGuid().ToString();
            var expirationTime = TimeSpan.FromMinutes(5);
            var now = DateTimeOffset.UtcNow;

            await _redisFixture.GetDatabase().StringSetAsync(accountId, DateTimeOffset.UtcNow.AddMonths(-1).ToUnixTimeSeconds(), TimeSpan.FromHours(1));

            // Act
            await _tokenBlacklist!.AddAsync(accountId, expirationTime);

            // Assert
            var result = await _redisFixture.GetDatabase().StringGetAsync(accountId);
            var remainingTime = await _redisFixture.GetDatabase().KeyTimeToLiveAsync(accountId);

            Assert.True(result.HasValue, "An entry with a given account ID is not found.");
            Assert.InRange((expirationTime - remainingTime!).Value.TotalMinutes, -1, 1);

            var value = DateTimeOffset.FromUnixTimeSeconds(Convert.ToInt64(result.ToString()));
            Assert.InRange((value - now).TotalMinutes, -1, 1);
        }

        [Fact]
        public async Task DeleteAsync_WhenEntryWithAccountIdDoesNotExist_Should()
        {
            // Act
            await _tokenBlacklist!.DeleteAsync(Guid.NewGuid().ToString());
        }

        [Fact]
        public async Task DeleteAsync_WhenEntryWithAccountIdExists_ShouldDeleteEntry()
        {
            // Arrange
            var accountId = Guid.NewGuid().ToString();

            await _redisFixture.GetDatabase().StringSetAsync(accountId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), TimeSpan.FromMinutes(5));

            // Act
            await _tokenBlacklist!.DeleteAsync(accountId);

            // Assert
            var result = await _redisFixture.GetDatabase().StringGetAsync(accountId);
            Assert.False(result.HasValue, "An entry with a given account ID exists.");
        }

        private static readonly PostgreSqlConnectionOptions _placeholderPostgreSqlConnectionOptions = new PostgreSqlConnectionOptions()
        {
            Host = "",
            Port = 0,
            Database = "",
            Username = "",
            Password = "",
            Ssl = new SslOptions()
            {
                Enabled = false
            }
        };

        private static readonly RabbitMqConnectionOptions _placeholderRabbitMqConnectionOptions = new RabbitMqConnectionOptions()
        {
            Host = "",
            Port = 0,
            Username = "",
            Password = "",
            Ssl = new SslOptions()
            {
                Enabled = false
            }
        };
    }
}
