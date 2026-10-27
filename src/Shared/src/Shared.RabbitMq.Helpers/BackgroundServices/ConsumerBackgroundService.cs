using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Utilities;

namespace Shared.RabbitMq.Helpers.BackgroundServices
{
    /// <summary>
    /// Handles all RabbitMQ connection, channels and consumers in the application.
    /// </summary>
    public abstract class ConsumerBackgroundService : BackgroundService
    {
        private readonly RabbitMqConnectionOptions _connectionOptions;
        private readonly string? _caBundleValue;
        private readonly TimeSpan _healthCheckTime;
        private readonly TimeSpan _graceTime;
        private readonly string _queueName;
        private readonly ushort _prefetchCount;
        private readonly ILogger _logger;

        private ConnectionFactory? _connectionFactory;
        private IConnection? _connection;
        public IChannel? Channel { get; private set; }
        private string? _consumerTag;

        public ConsumerBackgroundService(
            RabbitMqConnectionOptions connectionOptions,
            string? caBundleValue,
            TimeSpan healthCheckTime,
            TimeSpan graceTime,
            string queueName,
            ushort prefetchCount,
            ILogger logger)
        {
            _connectionOptions = connectionOptions;
            _caBundleValue = caBundleValue;
            _healthCheckTime = healthCheckTime;
            _graceTime = graceTime;
            _queueName = queueName;
            _prefetchCount = prefetchCount;
            _logger = logger;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _connectionFactory = await Utility.InitializeConnectionFactoryAsync(
                    _connectionOptions,
                    _caBundleValue,
                    cancellationToken);
                await InitializeConnectionAsync(cancellationToken);

                await base.StartAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong. Message: {Message}",
                    exception.Message);

                throw;
            }
        }

        private async Task InitializeConnectionAsync(CancellationToken cancellationToken)
        {
            if (_connection != null)
            {
                if (_connection.IsOpen)
                {
                    await _connection.CloseAsync();
                }

                await _connection.DisposeAsync();
            }

            _connection = await _connectionFactory!.CreateConnectionAsync(cancellationToken);
            
            await InitializeChannelAsync(cancellationToken);
        }

        private async Task InitializeChannelAsync(CancellationToken cancellationToken)
        {
            if (Channel != null)
            {
                if (Channel.IsOpen)
                {
                    if (_consumerTag != null)
                    {
                        await Channel.BasicCancelAsync(_consumerTag);
                    }

                    await Channel.CloseAsync();
                }

                await Channel.DisposeAsync();
            }

            Channel = await _connection!.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
                cancellationToken);

            await DeclareExchangesAsync(cancellationToken);
            await DeclareQueuesAsync(cancellationToken);

            await Channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: _prefetchCount,
                global: false,
                cancellationToken: cancellationToken);

            var consumer = new AsyncEventingBasicConsumer(Channel);
            consumer.ReceivedAsync += ReceivedAsync;

            _consumerTag = await Channel.BasicConsumeAsync(
                queue: _queueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: cancellationToken);
        }

        /// <summary>
        /// Declares exchanges. Use the <see cref="ConsumerBackgroundService.Channel"/> property to declare the exchanges.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the declarations</param>
        protected abstract Task DeclareExchangesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Declares queues and bind them with the exchanges. Use the <see cref="ConsumerBackgroundService.Channel"/> property
        /// to declare the queues.
        /// </summary>
        /// <param name="cancellationToken">Token to cancel the declarations</param>
        protected abstract Task DeclareQueuesAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Is called when a message is received.
        /// </summary>
        /// <param name="obj">Sender</param>
        /// <param name="args">Delivery arguments</param>
        protected abstract Task ReceivedAsync(object obj, BasicDeliverEventArgs args);

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_healthCheckTime, stoppingToken);

                try
                {
                    if (_connection == null || !_connection.IsOpen)
                    {
                        await InitializeConnectionAsync(stoppingToken);
                    }
                    else if (Channel == null || !Channel.IsOpen)
                    {
                        await InitializeChannelAsync(stoppingToken);
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("The consumer background service is cancelled.");
                    
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Something went wrong while checking the connection and channel. The process will rerun in {CheckTime} seconds. Message: {Message}",
                        _healthCheckTime.TotalSeconds,
                        exception.Message);
                }
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            // Wait for a small amount of time in case there are still messages that are being processed
            if (Channel != null && Channel.IsOpen && _consumerTag != null)
            {
                await Channel.BasicCancelAsync(_consumerTag);
            }
            await Task.Delay(_graceTime);

            // Close channels and connection
            if (Channel != null)
            {
                if (Channel.IsOpen)
                {
                    await Channel.CloseAsync();
                }

                await Channel.DisposeAsync();
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
