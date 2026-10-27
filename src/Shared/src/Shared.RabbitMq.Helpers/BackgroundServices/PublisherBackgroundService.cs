using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Structures;
using Shared.RabbitMq.Helpers.Utilities;

namespace Shared.RabbitMq.Helpers.BackgroundServices
{
    /// <summary>
    /// Handles all RabbitMQ connection, channels and publishers in the application.
    /// </summary>
    public abstract class PublisherBackgroundService : BackgroundService
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly List<Publisher> _publishers;
        private readonly TimeSpan _retryPublishTime;
        private readonly TimeSpan _graceTime;
        private readonly ILogger _logger;

        private IConnection? _connection;
        private Dictionary<int, Message> _droppedMessages;

        public PublisherBackgroundService(
            RabbitMqConnectionOptions connectionOptions,
            string? caBundleValue,
            List<Publisher> publishers,
            TimeSpan retryPublishTime,
            TimeSpan graceTime,
            ILogger logger)
        {
            _connectionFactory = Utility.SetupConnectionFactory(connectionOptions, caBundleValue);
            _publishers = publishers;
            _retryPublishTime = retryPublishTime;
            _graceTime = graceTime;
            _logger = logger;

            _droppedMessages = new Dictionary<int, Message>();
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                await InitializeAsync(cancellationToken);

                await base.StartAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong. Message: {Message}",
                    exception.Message);

                throw;
            }
        }

        private async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (_connection != null)
            {
                if (_connection.IsOpen)
                {
                    await _connection.CloseAsync();
                }

                await _connection.DisposeAsync();
            }

            _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            foreach (var publisher in _publishers)
            {
                await publisher.InitializeAsync(_connection!, cancellationToken);
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay((int)_retryPublishTime.TotalMilliseconds, stoppingToken);
                
                foreach (var publisher in _publishers)
                {
                    // Check connection and channel statuses
                    try
                    {
                        if (_connection == null || !_connection.IsOpen)
                        {
                            await InitializeAsync(stoppingToken);
                        }
                        else if (publisher.Channel == null || !publisher.Channel!.IsOpen)
                        {
                            await publisher.InitializeAsync(_connection!, stoppingToken);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.LogWarning("The publisher background service is cancelled.");

                        throw;
                    }
                    catch (Exception exception)
                    {
                        _logger.LogError(exception, "Something went wrong while checking the connection and channel for {ServiceName}-{PublisherName}. The process will rerun in {RetryPubishTime} seconds. Message: {Message}",
                            publisher.ServiceName,
                            publisher.PublisherName,
                            _retryPublishTime.TotalSeconds,
                            exception.Message);
                    }

                    // Retry publishing messages that are not acknowledged or not delivered
                    foreach (var message in publisher.DroppedMessages.ToArray())
                    {
                        if (message.Value.RetryCount >= publisher.MaxRetry)
                        {
                            _droppedMessages.TryAdd(message.Value.GetHashCode(), message.Value);
                            publisher.DroppedMessages.TryRemove(message.Key, out var _);
                            continue;
                        }

                        ++message.Value.RetryCount;
                        await publisher.PublishMessageAsync(message.Value, stoppingToken);
                    }

                    // Save the messages that are dropped many times
                    if (_droppedMessages.Count > 0)
                    {
                        try
                        {
                            if (await SaveDroppedMessagesAsync(_droppedMessages.Values, _droppedMessages.Count, isShuttingDown: false, stoppingToken))
                            {
                                _droppedMessages.Clear();
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning("The saving dropped messages operation is cancelled. The process will rerun in the StopAsync method.");
                            
                            throw;
                        }
                        catch (Exception exception)
                        {
                            _logger.LogError(exception, "Something went wrong while saving the dropped messages for {ServiceName}-{PublisherName}. The process will rerun in {RetryPubishTime} seconds. Message: {Message}",
                                publisher.ServiceName,
                                publisher.PublisherName,
                                _retryPublishTime.TotalSeconds,
                                exception.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Saves the messages that are dropped.
        /// </summary>
        /// <param name="droppedMessages">Messages that are attempted to send to RabbitMQ many times but dropped</param>
        /// <param name="messageCount">Number of the dropped messages</param>
        /// <param name="isShuttingDown">Whether this is the last call for saving the dropped messages</param>
        /// <param name="cancellationToken">Token to cancel the saving process</param>
        /// <returns>Returns a bool value representing whether the messages are saved or not.</returns>
        protected abstract Task<bool> SaveDroppedMessagesAsync(
            IEnumerable<Message> droppedMessages,
            int messageCount,
            bool isShuttingDown,
            CancellationToken cancellationToken = default);

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Wait for a small amount of time in case RabbitMQ fires some events
            await Task.Delay(_graceTime);

            // Add the last messages that are pending or are dropped in the publishers
            // to the background service's dropped messages
            foreach (var publisher in _publishers)
            {
                foreach (var message in publisher.PendingMessages)
                {
                    _droppedMessages.TryAdd(message.Value.GetHashCode(), message.Value);
                }
                foreach (var message in publisher.DroppedMessages)
                {
                    _droppedMessages.TryAdd(message.Value.GetHashCode(), message.Value);
                }
            }

            if (_droppedMessages.Count > 0)
            {
                try
                {
                    await SaveDroppedMessagesAsync(_droppedMessages.Values, _droppedMessages.Count, isShuttingDown: true);
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, "Something went wrong while saving the dropped messages for the last time. Message: {Message}",
                        exception.Message);
                }
            }

            // Close channels and connection
            foreach (var publisher in _publishers)
            {
                if (publisher.Channel != null)
                {
                    if (publisher.Channel.IsOpen)
                    {
                        await publisher.Channel.CloseAsync();
                    }

                    await publisher.Channel.DisposeAsync();
                }
            }

            if (_connection != null)
            {
                if (_connection.IsOpen)
                {
                    await _connection.CloseAsync();
                }

                await _connection.DisposeAsync();
            }

            await base.StopAsync(cancellationToken);
        }
    }
}
