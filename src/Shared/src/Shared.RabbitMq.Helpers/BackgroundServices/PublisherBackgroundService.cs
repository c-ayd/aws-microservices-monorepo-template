using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Structures;
using Shared.RabbitMq.Helpers.Utilities;

namespace Shared.RabbitMq.Helpers.BackgroundServices
{
    public abstract class PublisherBackgroundService : BackgroundService
    {
        private readonly ConnectionFactory _connectionFactory;
        private readonly List<Publisher> _publishers;
        private readonly TimeSpan _retryPublishTime;
        private readonly TimeSpan _graceTime;
        private readonly ILogger _logger;

        private IConnection? _connection;
        private Dictionary<int, Message> _rejectedMessages;

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

            _rejectedMessages = new Dictionary<int, Message>();
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
                        _logger.LogCritical(exception, "Someting went wrong while checking the connection and channels for {PublisherName}. The process will rerun in {RetryPubishTime} seconds. Message: {Message}",
                            publisher.Name,
                            _retryPublishTime.TotalSeconds,
                            exception.Message);
                    }

                    // Retry publishing messages that are not acknowledged or not delivered
                    foreach (var message in publisher.DroppedMessages.ToArray())
                    {
                        if (message.Value.RetryCount >= publisher.MaxRetry)
                        {
                            _rejectedMessages.TryAdd(message.Value.GetHashCode(), message.Value);
                            publisher.DroppedMessages.TryRemove(message.Key, out var _);
                            continue;
                        }

                        ++message.Value.RetryCount;
                        await publisher.PublishMessageAsync(message.Value, stoppingToken);
                    }

                    // Save the messages that are rejected many times
                    if (_rejectedMessages.Count > 0)
                    {
                        try
                        {
                            await SaveRejectedMessagesAsync(_rejectedMessages.Values, isShuttingDown: false, stoppingToken);
                            _rejectedMessages.Clear();
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.LogWarning("The saving rejected messages operation is cancelled. The process will rerun in the StopAsync method.");
                            
                            throw;
                        }
                        catch (Exception exception)
                        {
                            _logger.LogCritical(exception, "Someting went wrong while saving the rejected messages for {PublisherName}. The process will rerun in {RetryPubishTime} seconds. Message: {Message}",
                                publisher.Name,
                                _retryPublishTime.TotalSeconds,
                                exception.Message);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Saves the messages that are rejected.
        /// </summary>
        /// <param name="rejectedMessages">Messages that are attempted to send to RabbitMQ many times but rejected</param>
        /// <param name="isShuttingDown">Whether this is the last call for saving the rejected messages</param>
        /// <param name="cancellationToken">Token to cancel the saving process</param>
        protected abstract Task SaveRejectedMessagesAsync(
            IEnumerable<Message> rejectedMessages,
            bool isShuttingDown,
            CancellationToken cancellationToken = default);

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Wait for a small amount of time in case RabbitMQ fires some events
            await Task.Delay(_graceTime);

            // Add the last messages that are pending or are dropped to the rejected messages
            foreach (var publisher in _publishers)
            {
                foreach (var message in publisher.PendingMessages)
                {
                    _rejectedMessages.TryAdd(message.Value.GetHashCode(), message.Value);
                }
                foreach (var message in publisher.DroppedMessages)
                {
                    _rejectedMessages.TryAdd(message.Value.GetHashCode(), message.Value);
                }
            }

            if (_rejectedMessages.Count > 0)
            {
                try
                {
                    await SaveRejectedMessagesAsync(_rejectedMessages.Values, isShuttingDown: true);
                }
                catch (Exception exception)
                {
                    _logger.LogCritical(exception, "Someting went wrong while saving the rejected messages for the last time. Message: {Message}",
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
