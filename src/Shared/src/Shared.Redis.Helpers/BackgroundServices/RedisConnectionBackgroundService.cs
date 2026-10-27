using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shared.Redis.Helpers.BackgroundServices
{
    /// <summary>
    /// Handles all Redis connections in the application.
    /// </summary>
    public abstract class RedisConnectionBackgroundService : IHostedService
    {
        private readonly List<RedisConnection> _redisConnections;
        private readonly ILogger _logger;

        public RedisConnectionBackgroundService(
            List<RedisConnection> redisConnections,
            ILogger logger)
        {
            _redisConnections = redisConnections;
            _logger = logger;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                foreach (var redisConnection in _redisConnections)
                {
                    await redisConnection.ConnectAsync(cancellationToken);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong. Message: {Message}",
                    exception.Message);

                throw;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var redisConnection in _redisConnections)
            {
                if (redisConnection.Connection != null)
                {
                    await redisConnection.Connection.CloseAsync();
                    await redisConnection.Connection.DisposeAsync();
                }
            }
        }
    }
}
