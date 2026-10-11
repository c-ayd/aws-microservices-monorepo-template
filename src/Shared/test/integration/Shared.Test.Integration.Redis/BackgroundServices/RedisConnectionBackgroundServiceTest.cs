using Shared.Redis;
using Shared.Redis.BackgroundService;
using Shared.Redis.Options.Extensions;
using Shared.Test.Helpers.Fixtures;

namespace Shared.Test.Integration.Redis.BackgroundServices
{
    public class RedisConnectionBackgroundServiceTest :
        IClassFixture<RedisFixture>,
        IClassFixture<LoggerFixture<RedisConnectionBackgroundServiceTest>>,
        IAsyncLifetime
    {
        private RedisFixture _redisFixture;
        private LoggerFixture<RedisConnectionBackgroundServiceTest> _logger;

        public RedisConnectionBackgroundServiceTest(
            RedisFixture redisFixture,
            LoggerFixture<RedisConnectionBackgroundServiceTest> logger)
        {
            _redisFixture = redisFixture;
            _logger = logger;
        }

        public async Task InitializeAsync()
        {
            await _redisFixture.InitializeAsync(
                sslEnabled: true,
                caBundlePath: "./Certs/test-ca-bundle.crt",
                redisCertificateFolder: "./Certs/Redis");
        }

        public async Task DisposeAsync()
        {
            await _redisFixture.DisposeAsync();
        }

        [Fact]
        public async Task StartAsync_WhenMethodIsCalled_ShouldCreateConnection()
        {
            // Arrange
            var redisConnection = new TestRedisConnection(_redisFixture.GetConnectionOptions());
            var backgroundService = new TestBackgroundService(redisConnection, _logger);

            // Act
            await backgroundService.StartAsync(default);

            // Assert
            Assert.True(await redisConnection.CheckConnection());
        }

        [Fact]
        public async Task StopAsync_WhenMethodIsCalled_ShouldCloseConnection()
        {
            // Arrange
            var redisConnection = new TestRedisConnection(_redisFixture.GetConnectionOptions());
            var backgroundService = new TestBackgroundService(redisConnection, _logger);

            await backgroundService.StartAsync(default);

            // Act
            await backgroundService.StopAsync(default);

            // Assert
            Assert.False(await redisConnection.CheckConnection());
        }

        public class TestBackgroundService : RedisConnectionBackgroundService
        {
            public TestBackgroundService(
                TestRedisConnection testRedisConnection,
                LoggerFixture<RedisConnectionBackgroundServiceTest> logger)
                : base(
                redisConnections: new List<RedisConnection>()
                {
                    testRedisConnection
                },
                logger)
            {
            }
        }

        public class TestRedisConnection : RedisConnection
        {
            public TestRedisConnection(RedisConnectionOptions connectionOptions) : base(connectionOptions)
            {
            }

            public async Task<bool> CheckConnection()
            {
                try
                {
                    await GetDatabase().StringSetAsync("key", "test-value", TimeSpan.FromHours(1));
                    var value = await GetDatabase().StringGetAsync("key");
                    return value.HasValue;
                }
                catch
                {
                    return false;
                }
            }
        }
    }
}
