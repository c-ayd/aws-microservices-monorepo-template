using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Utilities;

namespace RecoveryService.Worker.BackgroundServices.SeedWork
{
    public abstract class RepublishBackgroundServiceBase : BackgroundService
    {
        private readonly TimeSpan _sleepTime;
        private readonly ILogger _logger;

        private readonly ConnectionFactory _connectionFactory;
        private IConnection? _connection;
        protected IChannel? Channel { get; private set; }

        public RepublishBackgroundServiceBase(
            RabbitMqConnectionOptions connectionOptions,
            string? caBundleValue,
            TimeSpan sleepTime,
            ILogger logger)
        {
            _sleepTime = sleepTime;
            _logger = logger;
            
            _connectionFactory = Utility.SetupConnectionFactory(connectionOptions, caBundleValue);
            _connectionFactory.AutomaticRecoveryEnabled = true;
        }

        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                _connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
                Channel = await _connection.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: true,
                    publisherConfirmationTrackingEnabled: true));

                await base.StartAsync(cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong. Message: {Message}",
                    exception.Message);

                throw;
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(_sleepTime, stoppingToken);

                try
                {
                    await RepublishAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    _logger.LogWarning("The recovering rejected messages from database process is cancelled.");

                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Something went wrong while recovering rejected messages from the DB. The process will rerun in {SleepTime} seconds. Message: {Message}",
                        _sleepTime.TotalSeconds,
                        exception.Message);
                }
            }
        }

        protected abstract Task RepublishAsync(CancellationToken cancellationToken);

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
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
