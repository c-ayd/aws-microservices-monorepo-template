using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.RabbitMq.Helpers.BackgroundServices;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.Test.Generators;
using Shared.Test.Helpers.Fixtures;
using Shared.Test.Integration.RabbitMq.Helpers.Collections;

namespace Shared.Test.Integration.RabbitMq.Helpers.BackgroundServices
{
    [Collection(nameof(RabbitMqCollection))]
    public class ConsumerBackgroundServiceTest : IClassFixture<LoggerFixture<ConsumerBackgroundServiceTest>>
    {
        private readonly TimeSpan _timeoutTime = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _healthCheckTime = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _graceTime = TimeSpan.FromSeconds(1);

        private const string _exchangeName = "shared.rabbitmq.helpers.consumerbackgroundservice.exchange";
        private const string _routingKey = "shared.rabbitmq.helpers.consumerbackgroundservice.routing";
        private const string _queueName = "shared.rabbitmq.helpers.consumerbackgroundservice.queue";

        private readonly RabbitMqFixture _rabbitMqFixture;
        private readonly LoggerFixture<ConsumerBackgroundServiceTest> _loggerFixture;

        private string _caBundle;

        public ConsumerBackgroundServiceTest(
            RabbitMqCollectionCluster rabbitMqCollectionCluster,
            LoggerFixture<ConsumerBackgroundServiceTest> loggerFixture)
        {
            _rabbitMqFixture = rabbitMqCollectionCluster.RabbitMqFixture;
            _loggerFixture = loggerFixture;

            _caBundle = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Certs/test-ca-bundle.crt"));
        }

        [Fact]
        public async Task StartAsync_WhenMethodIsCalled_ShouldCreateConnnectionAndChannelAndConsumer()
        {
            // Arrange
            var backgroundService = new TestBackgroundService(
                _rabbitMqFixture.GetConnectionOptions(),
                _caBundle,
                TimeSpan.FromSeconds(60),
                _graceTime,
                _queueName,
                1,
                _loggerFixture);

            // Act
            var cts = new CancellationTokenSource();
            await backgroundService.StartAsync(cts.Token);
            await cts.CancelAsync();

            // Assert
            Assert.NotNull(GetConnection(backgroundService));
            Assert.NotNull(backgroundService.Channel);
            Assert.NotNull(GetConsumerTag(backgroundService));

            await backgroundService.StopAsync(default);
        }

        [Fact]
        public async Task ExecuteAsync_WhenConnectionIsStable_ShouldCallReceivedEvent()
        {
            // Arrange
            var backgroundService = new TestBackgroundService(
                _rabbitMqFixture.GetConnectionOptions(),
                _caBundle,
                _healthCheckTime,
                _graceTime,
                _queueName,
                1,
                _loggerFixture);
            await InitializeBackgroundService(backgroundService);

            var messageCount = await _rabbitMqFixture.GetMessageCountAsync(_queueName);
            if (messageCount != 0)
                Assert.Fail($"The queue is not empty. Count: {messageCount}");

            var message = StringGenerator.GeneratePrintableAscii();
            await _rabbitMqFixture.PublishMessageAsync(
                _exchangeName,
                _routingKey,
                new BasicProperties(),
                JsonSerializer.SerializeToUtf8Bytes(message));

            // Act
            var cts = new CancellationTokenSource();
            var executeTask = Task.Run(() => ExecuteAsync(backgroundService, cts.Token));

            var result = await backgroundService.ReceivedTcs.Task.WaitAsync(_timeoutTime);
            await cts.CancelAsync();

            // Assert
            Assert.True(result, "The received event did not set the value to true.");

            Assert.Equal(message, backgroundService.Message);

            await backgroundService.StopAsync(default);
        }
        
        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task ExecuteAsync_WhenConnectionOrChannelIsClosed_ShouldRenewConnectionOrChannel(bool isConnectionClosed, bool isChannelClosed)
        {
            // Arrange
            var backgroundService = new TestBackgroundService(
                _rabbitMqFixture.GetConnectionOptions(),
                _caBundle,
                _healthCheckTime,
                _graceTime,
                _queueName,
                1,
                _loggerFixture);
            await InitializeBackgroundService(backgroundService);

            var messageCount = await _rabbitMqFixture.GetMessageCountAsync(_queueName);
            if (messageCount != 0)
                Assert.Fail($"The queue is not empty. Count: {messageCount}");

            var consumerTag = GetConsumerTag(backgroundService);

            if (isConnectionClosed)
            {
                await CloseConnection(backgroundService);
            }
            else if (isChannelClosed)
            {
                await CloseChannel(backgroundService);
            }

            var message = StringGenerator.GeneratePrintableAscii();
            await _rabbitMqFixture.PublishMessageAsync(
                _exchangeName,
                _routingKey,
                new BasicProperties(),
                JsonSerializer.SerializeToUtf8Bytes(message));

            // Act
            var cts = new CancellationTokenSource();
            var executeTask = Task.Run(() => ExecuteAsync(backgroundService, cts.Token));

            var result = await backgroundService.ReceivedTcs.Task.WaitAsync(_timeoutTime);
            await cts.CancelAsync();

            // Assert
            Assert.NotEqual(consumerTag, GetConsumerTag(backgroundService));
            Assert.NotNull(GetConnection(backgroundService));
            Assert.NotNull(backgroundService.Channel);

            await backgroundService.StopAsync(default);
        }

        [Fact]
        public async Task StopAsync_WhenApplicationShutsDown_ShouldCloseConnectionAndChannel()
        {
            // Arrange
            var backgroundService = new TestBackgroundService(
                _rabbitMqFixture.GetConnectionOptions(),
                _caBundle,
                _healthCheckTime,
                _graceTime,
                _queueName,
                1,
                _loggerFixture);
            await InitializeBackgroundService(backgroundService);

            // Act
            await backgroundService.StopAsync(default);

            // Assert
            Assert.False(GetConnection(backgroundService).IsOpen, "The connection is still open.");
            Assert.False(backgroundService.Channel!.IsOpen, "The channel is still open.");
        }

        private async Task InitializeBackgroundService(TestBackgroundService backgroundService)
        {
            var initializeConnectionMethodInfo = typeof(ConsumerBackgroundService).GetMethod("InitializeConnectionAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)initializeConnectionMethodInfo.Invoke(backgroundService, [default])!;
        }

        private IConnection GetConnection(TestBackgroundService backgroundService)
        {
            var fieldInfo = typeof(ConsumerBackgroundService).GetField("_connection", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (IConnection)fieldInfo.GetValue(backgroundService)!;
        }

        private async Task CloseConnection(TestBackgroundService backgroundService)
        {
            var connection = GetConnection(backgroundService);
            if (connection != null)
            {
                await connection.CloseAsync();
            }
        }

        private async Task CloseChannel(TestBackgroundService backgroundService)
        {
            if (backgroundService.Channel != null)
            {
                await backgroundService.Channel.CloseAsync();
            }
        }

        private string GetConsumerTag(TestBackgroundService backgroundService)
        {
            var fieldInfo = typeof(ConsumerBackgroundService).GetField("_consumerTag", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (string)fieldInfo.GetValue(backgroundService)!;
        }

        private Task ExecuteAsync(TestBackgroundService backgroundService, CancellationToken cancellationToken)
        {
            var methodInfo = typeof(ConsumerBackgroundService).GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Task)methodInfo.Invoke(backgroundService, [cancellationToken])!;
        }

        private class TestBackgroundService : ConsumerBackgroundService
        {
            public TaskCompletionSource<bool> ReceivedTcs { get; private set; }
            public string? Message { get; private set; }

            public TestBackgroundService(
                RabbitMqConnectionOptions connectionOptions,
                string caBundleValue,
                TimeSpan healthCheckTime,
                TimeSpan graceTime,
                string queueName,
                ushort prefetchCount,
                ILogger logger)
                : base(connectionOptions, caBundleValue, healthCheckTime, graceTime, queueName, prefetchCount, logger)
            {
                ReceivedTcs = new TaskCompletionSource<bool>();
            }

            protected override async Task DeclareExchangesAsync(CancellationToken cancellationToken)
            {
                await Channel!.ExchangeDeclareAsync(
                    exchange: _exchangeName,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);
            }

            protected override async Task DeclareQueuesAsync(CancellationToken cancellationToken)
            {
                await Channel!.QueueDeclareAsync(
                    queue: _queueName,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);
                await Channel.QueueBindAsync(
                    queue: _queueName,
                    exchange: _exchangeName,
                    routingKey: _routingKey);
            }

            protected override async Task ReceivedAsync(object obj, BasicDeliverEventArgs args)
            {
                Message = JsonSerializer.Deserialize<string>(args.Body.ToArray());

                await Channel!.BasicAckAsync(args.DeliveryTag, multiple: false);

                ReceivedTcs.SetResult(true);
            }
        }
    }
}
