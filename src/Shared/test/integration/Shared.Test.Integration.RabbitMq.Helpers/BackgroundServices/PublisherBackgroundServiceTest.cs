using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.RabbitMq.Helpers;
using Shared.RabbitMq.Helpers.BackgroundServices;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Structures;
using Shared.Test.Generators;
using Shared.Test.Helpers.Fixtures;
using Shared.Test.Integration.RabbitMq.Helpers.Collections;

namespace Shared.Test.Integration.RabbitMq.Helpers.BackgroundServices
{
    [Collection(nameof(RabbitMqCollection))]
    public class PublisherBackgroundServiceTest : IClassFixture<LoggerFixture<PublisherBackgroundServiceTest>>
    {
        private readonly TimeSpan _timeoutTime = TimeSpan.FromSeconds(5);
        private readonly TimeSpan _retryTime = TimeSpan.FromSeconds(1);
        private readonly TimeSpan _graceTime = TimeSpan.FromSeconds(1);

        private const int _maxRetry = 3;

        private const string _normalExchange = "shared.rabbitmq.helpers.publisherbackgroundservice.exchange";
        private const string _normalRouting = "shared.rabbitmq.helpers.publisherbackgroundservice.routing.normal";
        public const string _normalQueue = "shared.rabbitmq.helpers.publisherbackgroundservice.queue";

        private const string _rejectExchange = "shared.rabbitmq.helpers.publisherbackgroundservice.exchange.reject";
        private const string _rejectRouting = "shared.rabbitmq.helpers.publisherbackgroundservice.routing.reject";
        public const string _rejectQueue = "shared.rabbitmq.helpers.publisherbackgroundservice.queue.reject";

        private readonly RabbitMqCollectionCluster _collectionCluster;
        private readonly LoggerFixture<PublisherBackgroundServiceTest> _logger;
        private string _caBundleValue;

        public PublisherBackgroundServiceTest(
            RabbitMqCollectionCluster collectionCluster,
            LoggerFixture<PublisherBackgroundServiceTest> logger)
        {
            _collectionCluster = collectionCluster;
            _logger = logger;

            _caBundleValue = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, RabbitMqCollectionCluster.CABundlePath));
        }

        [Fact]
        public async Task StartAsync_WhenMethodIsCalled_ShouldCreateConnnectionAndChannels()
        {
            // Arrange
            var publisher = new TestPublisher();
            var backgroundService = new TestBackgroundService(
                _collectionCluster.RabbitMqFixture.GetConnectionOptions(),
                _caBundleValue,
                new List<Publisher>() { publisher },
                _retryTime,
                _graceTime,
                _logger
            );

            // Act
            var cts = new CancellationTokenSource();
            await backgroundService.StartAsync(cts.Token);
            await cts.CancelAsync();

            // Assert
            Assert.NotNull(GetConnection(backgroundService));
            Assert.NotNull(publisher.Channel);

            await backgroundService.StopAsync(default);
        }

        [Fact]
        public async Task ExecuteAsync_WhenThereIsDroppedMessage_ShouldRetryPublishing()
        {
            // Arrange
            var messageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(_normalQueue);
            if (messageCount != 0)
                Assert.Fail($"The normal queue is not empty. Count: {messageCount}");

            var publisher = new TestPublisher();
            var backgroundService = new TestBackgroundService(
                _collectionCluster.RabbitMqFixture.GetConnectionOptions(),
                _caBundleValue,
                new List<Publisher>() { publisher },
                _retryTime,
                _graceTime,
                _logger
            );

            var droppedMessage = new Message(
                RabbitMqCollectionCluster.TestServiceName,
                RabbitMqCollectionCluster.TestPublisherName,
                _normalExchange,
                _normalRouting,
                new BasicProperties()
                {
                    CorrelationId = Guid.NewGuid().ToString()
                },
                body: JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GeneratePrintableAscii()));
            
            GetDroppedMessages(publisher).TryAdd(droppedMessage.GetHashCode(), droppedMessage);

            var acknowledgeEventTcs = new TaskCompletionSource<bool>();
            await InitializeBackgroundServiceAndPublisherAsync(
                backgroundService,
                publisher,
                acknowledgeEvent: async (obj, args) =>
                {
                    acknowledgeEventTcs.SetResult(true);
                });

            // Act
            var cts = new CancellationTokenSource();
            var executeTask = Task.Run(() => ExecuteAsync(backgroundService, cts.Token));

            var result = await acknowledgeEventTcs.Task.WaitAsync(_timeoutTime);
            await cts.CancelAsync();

            // Assert
            Assert.True(result, "The acknowledge event did not set the value to true.");

            Assert.Empty(GetDroppedMessages(publisher));
            Assert.Equal((uint)1, await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(_normalQueue));

            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(_normalQueue);
        }

        [Fact]
        public async Task ExecuteAsync_WhenThereIsMessagesThatExceedRetryLimit_ShouldRemoveMessageFromPublisherAndPutItInBackgroundServiceAndCallSaveDroppedMessagesMethod()
        {
            // Arrange
            var publisher = new TestPublisher();
            var backgroundService = new TestBackgroundService(
                _collectionCluster.RabbitMqFixture.GetConnectionOptions(),
                _caBundleValue,
                new List<Publisher>() { publisher },
                _retryTime,
                _graceTime,
                _logger
            );

            var droppedMessage = new Message(
                RabbitMqCollectionCluster.TestServiceName,
                RabbitMqCollectionCluster.TestPublisherName,
                _rejectExchange,
                _rejectRouting,
                new BasicProperties()
                {
                    CorrelationId = Guid.NewGuid().ToString()
                },
                JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GeneratePrintableAscii()));
            SetRetryCount(droppedMessage, 1);

            GetDroppedMessages(publisher).TryAdd(droppedMessage.GetHashCode(), droppedMessage);

            int counter = 1;
            var notAcknowledgeEventTcs = new TaskCompletionSource<bool>();
            await InitializeBackgroundServiceAndPublisherAsync(
                backgroundService,
                publisher,
                notAcknowledgeEvent: async (obj, args) =>
                {
                    ++counter;
                    if (counter >= _maxRetry)
                    {
                        notAcknowledgeEventTcs.SetResult(true);
                    }
                });

            // Act
            var cts = new CancellationTokenSource();
            var executeTask = Task.Run(() => ExecuteAsync(backgroundService, cts.Token));

            var result = await notAcknowledgeEventTcs.Task.WaitAsync(_timeoutTime);
            await Task.Delay(_timeoutTime); // Wait for the next the message to be in the dropped messages
            await cts.CancelAsync();

            // Assert
            Assert.True(result, "The not acknowledge event did not set the value to true.");

            Assert.Empty(GetDroppedMessages(publisher));

            Assert.Single(backgroundService.DroppedMessages);
            Assert.Contains(droppedMessage.GetHashCode(), backgroundService.DroppedMessages.Select(m => m.GetHashCode()));
            Assert.False(backgroundService.IsShuttingDown, "The shutdown parameter was given as true.");
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        public async Task ExecuteAsync_WhenConnectionOrChannelIsClosed_ShouldRenewConnectionOrChannel(bool isConnectionClosed, bool isChannelClosed)
        {
            // Arrange
            var publisher = new TestPublisher();
            var backgroundService = new TestBackgroundService(
                _collectionCluster.RabbitMqFixture.GetConnectionOptions(),
                _caBundleValue,
                new List<Publisher>() { publisher },
                _retryTime,
                _graceTime,
                _logger
            );

            await InitializeBackgroundServiceAndPublisherAsync(backgroundService, publisher);

            if (isConnectionClosed)
            {
                await CloseConnectionAsync(backgroundService);
            }
            else if (isChannelClosed)
            {
                await CloseChannelAsync(publisher);
            }

            // Act
            var cts = new CancellationTokenSource();
            var executeTask = Task.Run(() => ExecuteAsync(backgroundService, cts.Token));

            await Task.Delay(_timeoutTime);

            // Assert
            Assert.True(GetConnection(backgroundService).IsOpen, "The connection is not open.");
            Assert.True(publisher.Channel!.IsOpen, "The channel is not open.");

            await cts.CancelAsync();
        }

        [Fact]
        public async Task StopAsync_WhenApplicationShutsDown_ShouldCombineLeftOverMessagesAndCallSaveDroppedMessagesAndCloseConnectionAndChannels()
        {
            // Arrange
            var publisher = new TestPublisher();
            var backgroundService = new TestBackgroundService(
                _collectionCluster.RabbitMqFixture.GetConnectionOptions(),
                _caBundleValue,
                new List<Publisher>() { publisher },
                _retryTime,
                _graceTime,
                _logger
            );

            var pendingMessage = new Message(
                RabbitMqCollectionCluster.TestServiceName,
                RabbitMqCollectionCluster.TestPublisherName,
                _normalExchange,
                _normalRouting,
                new BasicProperties()
                {
                    CorrelationId = Guid.NewGuid().ToString()
                },
                body: JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GeneratePrintableAscii()));
            var droppedMessage = new Message(
                RabbitMqCollectionCluster.TestServiceName,
                RabbitMqCollectionCluster.TestPublisherName,
                _normalExchange,
                _normalRouting,
                new BasicProperties()
                {
                    CorrelationId = Guid.NewGuid().ToString()
                },
                body: JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GeneratePrintableAscii()));
            var droppedMessageInBackgroundService = new Message(
                RabbitMqCollectionCluster.TestServiceName,
                RabbitMqCollectionCluster.TestPublisherName,
                _normalExchange,
                _normalRouting,
                new BasicProperties()
                {
                    CorrelationId = Guid.NewGuid().ToString()
                },
                body: JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GeneratePrintableAscii()));

            GetPendingMessages(publisher).TryAdd(0, pendingMessage);
            GetDroppedMessages(publisher).TryAdd(droppedMessage.GetHashCode(), droppedMessage);
            GetDroppedMessages(backgroundService).TryAdd(droppedMessageInBackgroundService.GetHashCode(), droppedMessageInBackgroundService);

            await InitializeBackgroundServiceAndPublisherAsync(backgroundService, publisher);

            // Act
            await backgroundService.StopAsync(default);

            // Assert
            Assert.Equal(3, backgroundService.DroppedMessages.Count);
            Assert.Contains(pendingMessage.GetHashCode(), backgroundService.DroppedMessages.Select(m => m.GetHashCode()));
            Assert.Contains(droppedMessage.GetHashCode(), backgroundService.DroppedMessages.Select(m => m.GetHashCode()));
            Assert.Contains(droppedMessageInBackgroundService.GetHashCode(), backgroundService.DroppedMessages.Select(m => m.GetHashCode()));

            Assert.True(backgroundService.IsShuttingDown, "The shutdown parameter was given as true.");
            Assert.False(GetConnection(backgroundService).IsOpen, "The connection is still open.");
            Assert.False(publisher.Channel!.IsOpen, "The channel is still open.");
        }

        private async Task InitializeBackgroundServiceAndPublisherAsync(
            TestBackgroundService backgroundService,
            TestPublisher publisher,
            AsyncEventHandler<BasicAckEventArgs>? acknowledgeEvent = null,
            AsyncEventHandler<BasicNackEventArgs>? notAcknowledgeEvent = null,
            AsyncEventHandler<BasicReturnEventArgs>? returnEvent = null)
        {
            // Backgroung service
            var connFactoryfieldInfo = typeof(PublisherBackgroundService)
                .GetField("_connectionFactory", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var connFactory = (ConnectionFactory)connFactoryfieldInfo.GetValue(backgroundService)!;
            var connection = await connFactory.CreateConnectionAsync();

            var connectionFieldInfo = typeof(PublisherBackgroundService)
                .GetField("_connection", BindingFlags.NonPublic | BindingFlags.Instance)!;
            connectionFieldInfo.SetValue(backgroundService, connection);

            // Publisher
            var channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: false));

            var acknowledgeMethodInfo = typeof(Publisher)
                .GetMethod("HandleAcknowledgedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var mainAcknowledgeEvent = Delegate.CreateDelegate(typeof(AsyncEventHandler<BasicAckEventArgs>), publisher, acknowledgeMethodInfo);

            var notAcknowledgeMethodInfo = typeof(Publisher)
                .GetMethod("HandleNotAcknowledgedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var mainNotAcknowledgeEvent = Delegate.CreateDelegate(typeof(AsyncEventHandler<BasicNackEventArgs>), publisher, notAcknowledgeMethodInfo);

            var returnMethodInfo = typeof(Publisher)
                .GetMethod("HandleReturnedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var mainReturnEvent = Delegate.CreateDelegate(typeof(AsyncEventHandler<BasicReturnEventArgs>), publisher, returnMethodInfo);

            channel.BasicAcksAsync += (AsyncEventHandler<BasicAckEventArgs>)mainAcknowledgeEvent;
            channel.BasicNacksAsync += (AsyncEventHandler<BasicNackEventArgs>)mainNotAcknowledgeEvent;
            channel.BasicReturnAsync += (AsyncEventHandler<BasicReturnEventArgs>)mainReturnEvent;
            channel.BasicAcksAsync += acknowledgeEvent;
            channel.BasicNacksAsync += notAcknowledgeEvent;
            channel.BasicReturnAsync += returnEvent;

            var channelPropertyInfo = typeof(Publisher)
                .GetProperty("Channel", BindingFlags.Public | BindingFlags.Instance)!;
            channelPropertyInfo.SetValue(publisher, channel);

            var declareExchangeMethodInfo = typeof(Publisher)
                .GetMethod("DeclareExchangesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)declareExchangeMethodInfo.Invoke(publisher, [default])!;
        }

        private IConnection GetConnection(TestBackgroundService backgroundService)
        {
            var fieldInfo = typeof(PublisherBackgroundService)
                .GetField("_connection", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (IConnection)fieldInfo.GetValue(backgroundService)!;
        }

        private async Task CloseConnectionAsync(TestBackgroundService backgroundService)
        {
            var connection = GetConnection(backgroundService);
            if (connection != null)
            {
                await connection.CloseAsync();
            }
        }

        private async Task CloseChannelAsync(TestPublisher publisher)
        {
            if (publisher.Channel != null)
            {
                await publisher.Channel.CloseAsync();
            }
        }

        private Task ExecuteAsync(TestBackgroundService backgroundService, CancellationToken cancellationToken)
        {
            var methodInfo = typeof(PublisherBackgroundService)
                .GetMethod("ExecuteAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Task)methodInfo.Invoke(backgroundService, [cancellationToken])!;
        }

        private ConcurrentDictionary<ulong, Message> GetPendingMessages(TestPublisher publisher)
        {
            var propertyInfo = typeof(Publisher)
                .GetProperty("PendingMessages", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (ConcurrentDictionary<ulong, Message>)propertyInfo.GetValue(publisher)!;
        }

        private ConcurrentDictionary<int, Message> GetDroppedMessages(TestPublisher publisher)
        {
            var propertyInfo = typeof(Publisher)
                .GetProperty("DroppedMessages", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (ConcurrentDictionary<int, Message>)propertyInfo.GetValue(publisher)!;
        }

        private void SetRetryCount(Message message, int value)
        {
            var propertyInfo = typeof(Message)
                .GetProperty("RetryCount", BindingFlags.NonPublic | BindingFlags.Instance)!;
            propertyInfo.SetValue(message, value);
        }

        private Dictionary<int, Message> GetDroppedMessages(TestBackgroundService backgroundService)
        {
            var fieldInfo = typeof(PublisherBackgroundService)
                .GetField("_droppedMessages", BindingFlags.NonPublic | BindingFlags.Instance)!;
            return (Dictionary<int, Message>)fieldInfo.GetValue(backgroundService)!;
        }

        private class TestBackgroundService : PublisherBackgroundService
        {
            public List<Message> DroppedMessages { get; private set; }
            public bool IsShuttingDown { get; private set; }

            public TestBackgroundService(
                RabbitMqConnectionOptions connectionOptions,
                string caBundleValue,
                List<Publisher> publishers,
                TimeSpan publishRetryTime,
                TimeSpan graceTime,
                ILogger logger)
                : base(connectionOptions, caBundleValue, publishers, publishRetryTime, graceTime, logger)
            {
                DroppedMessages = new List<Message>();
            }

            protected override async Task<bool> SaveDroppedMessagesAsync(
                IEnumerable<Message> droppedMessages,
                int messageCount,
                bool isShuttingDown,
                CancellationToken cancellationToken = default)
            {
                DroppedMessages.AddRange(droppedMessages);
                IsShuttingDown = isShuttingDown;
                return true;
            }
        }

        private class TestPublisher : Publisher
        {
            public TestPublisher()
                : base(
                RabbitMqCollectionCluster.TestServiceName,
                RabbitMqCollectionCluster.TestPublisherName,
                _maxRetry)
            {
            }

            protected override async Task DeclareExchangesAsync(CancellationToken cancellationToken)
            {
                await Channel!.ExchangeDeclareAsync(
                    exchange: _normalExchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                await Channel.ExchangeDeclareAsync(
                    exchange: _rejectExchange,
                    type: ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                // Declare queues for test
                await Channel.QueueDeclareAsync(
                    queue: _normalQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);
                await Channel.QueueBindAsync(
                    queue: _normalQueue,
                    exchange: _normalExchange,
                    routingKey: _normalRouting);

                await Channel.QueueDeclareAsync(
                    queue: _rejectQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object?>()
                    {
                        { "x-max-length", 0 },
                        { "x-overflow", "reject-publish" }
                    });
                await Channel.QueueBindAsync(
                    queue: _rejectQueue,
                    exchange: _rejectExchange,
                    routingKey: _rejectRouting);
            }
        }
    }
}
