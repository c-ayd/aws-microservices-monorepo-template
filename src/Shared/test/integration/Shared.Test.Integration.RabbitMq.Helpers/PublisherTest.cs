using System.Collections.Concurrent;
using System.Reflection;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.RabbitMq.Helpers;
using Shared.RabbitMq.Helpers.Structures;
using Shared.Test.Generators;
using Shared.Test.Integration.RabbitMq.Helpers.Collections;

namespace Shared.Test.Integration.RabbitMq.Helpers
{
    [Collection(nameof(RabbitMqCollection))]
    public class PublisherTest
    {
        private const string _normalExchange = "shared.rabbitmq.helpers.publisher.exchange";
        private const string _normalRouting = "shared.rabbitmq.helpers.publisher.routing.normal";
        public const string _normalQueue = "shared.rabbitmq.helpers.publisher.queue";

        private const string _rejectExchange = "shared.rabbitmq.helpers.publisher.exchange.reject";
        private const string _rejectRouting = "shared.rabbitmq.helpers.publisher.routing.reject";
        public const string _rejectQueue = "shared.rabbitmq.helpers.publisher.queue.reject";

        private const string _noQueueExchange = "shared.rabbitmq.helpers.publisher.exchange.no-queue";

        private readonly Dictionary<string, object?> _testHeaders = new Dictionary<string, object?>
        {
            { "Key1", 10 },
            { "Key2", "Test value" },
            { "Key3", true },
            { "Key4", 10.10 },
            { "Key5", new int[] { 1, 2, 3, 4, 5 } },
            { "Key6", new byte[] { 5, 4, 3, 2, 1 } },
            { "Key7", new TestClass() },
            { "Key8", new Dictionary<int, string>() { { 1, "abc" }, { 2, "def" } } },
            { "Key9", null }
        };

        private readonly TimeSpan _timeoutSpan = TimeSpan.FromSeconds(5);

        private readonly RabbitMqCollectionCluster _collectionCluster;

        public PublisherTest(RabbitMqCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task PublishMessageAsync_WhenMessageIsNotRouted_ShouldAddMessageToDroppedMessagesWithCorrectHeaders()
        {
            // Arrange
            var message = StringGenerator.GeneratePrintableAscii();
            var properties = new BasicProperties()
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Headers = new Dictionary<string, object?>(_testHeaders)
            };

            var publisher = new TestPublisher();
            var returnEventTcs = new TaskCompletionSource<string>();
            var acknowledgeEventTcs = new TaskCompletionSource<bool>();
            await InitializePublisherAsync(publisher,  
                returnEvent: async (obj, args) =>
                {
                    returnEventTcs.SetResult(Encoding.UTF8.GetString(args.Body.ToArray()));
                },
                acknowledgeEvent: async (pbj, args) =>
                {
                    acknowledgeEventTcs.SetResult(true);
                });

            // Act
            await publisher.PublishMessageAsync(
                _noQueueExchange,
                routingKey: StringGenerator.GenerateAlpha(),
                properties,
                body: Encoding.UTF8.GetBytes(message));

            // Assert
            await Task.WhenAll(returnEventTcs.Task, acknowledgeEventTcs.Task);
            Assert.Equal(message, await returnEventTcs.Task);

            var pendingMessages = GetPendingMessages(publisher);
            var droppedMessages = GetDroppedMessages(publisher);
            Assert.Empty(pendingMessages);
            Assert.Single(droppedMessages);

            CheckHeaders(droppedMessages.Values.First());
        }

        [Fact]
        public async Task PublishMessageAsync_WhenMessageIsNotAcknowledged_ShouldRemoveMessageFromPendingAndPutItIntoDroppedMessages()
        {
            // Arrange
            var properties = new BasicProperties()
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Headers = new Dictionary<string, object?>(_testHeaders)
            };

            var publisher = new TestPublisher();
            var notAcknowledgeEventTcs = new TaskCompletionSource<bool>();
            await InitializePublisherAsync(publisher,
                notAcknowledgeEvent: async (obj, args) =>
                {
                    notAcknowledgeEventTcs.SetResult(true);
                });

            // Act
            await publisher.PublishMessageAsync(
                _rejectExchange,
                _rejectRouting,
                properties,
                body: Encoding.UTF8.GetBytes(StringGenerator.GeneratePrintableAscii()));

            // Assert
            var result = await notAcknowledgeEventTcs.Task.WaitAsync(_timeoutSpan);
            Assert.True(result, "The not acknowledge event did not set the value to true.");

            var pendingMessages = GetPendingMessages(publisher);
            var droppedMessages = GetDroppedMessages(publisher);
            Assert.Empty(pendingMessages);
            Assert.Single(droppedMessages);

            CheckHeaders(droppedMessages.Values.First());
        }

        [Fact]
        public async Task PublishMessageAsync_WhenMessageIsAcknowledged_ShouldRemoveMessageFromPendingMessages()
        {
            // Arrange
            var message = StringGenerator.GeneratePrintableAscii();
            var properties = new BasicProperties()
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Headers = new Dictionary<string, object?>(_testHeaders)
            };

            var publisher = new TestPublisher();
            var acknowledgeEventTcs = new TaskCompletionSource<bool>();
            await InitializePublisherAsync(publisher,
                acknowledgeEvent: async (obj, args) =>
                {
                    acknowledgeEventTcs.SetResult(true);
                });

            var messageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(_normalQueue);
            if (messageCount != 0)
                Assert.Fail($"The normal queue is not empty. Count: {messageCount}");

            // Act
            await publisher.PublishMessageAsync(
                _normalExchange,
                _normalRouting,
                properties,
                body: Encoding.UTF8.GetBytes(message));

            // Assert
            var result = await acknowledgeEventTcs.Task.WaitAsync(_timeoutSpan);
            Assert.True(result, "The acknowledge event did not set the value to true.");

            var pendingMessages = GetPendingMessages(publisher);
            var droppedMessages = GetDroppedMessages(publisher);
            Assert.Empty(pendingMessages);
            Assert.Empty(droppedMessages);

            var messageFromRabbitMq = await _collectionCluster.RabbitMqFixture.GetNextMessageAsync(_normalQueue);

            Assert.NotNull(messageFromRabbitMq);
            Assert.Equal(message, Encoding.UTF8.GetString(messageFromRabbitMq.Body.ToArray()));
            Assert.Equal(properties.CorrelationId, messageFromRabbitMq.BasicProperties.CorrelationId);

            CheckHeaders(new Message(
                nameof(TestPublisher),
                nameof(TestPublisher),
                _normalExchange,
                _normalRouting,
                new BasicProperties(messageFromRabbitMq.BasicProperties),
                isReturned: true,
                messageFromRabbitMq.Body.ToArray()));
        }

        [Fact]
        public async Task PublishMessageAsync_WhenChannelIsNotOpen_ShouldAddMessageToDroppedMessagesWithCorrectHeaders()
        {
            // Arrange
            var properties = new BasicProperties()
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Headers = new Dictionary<string, object?>(_testHeaders)
            };

            var publisher = new TestPublisher();
            await InitializePublisherAsync(publisher);

            var channelPropertyInfo = typeof(Publisher)
                .GetProperty("Channel", BindingFlags.Public | BindingFlags.Instance)!;
            channelPropertyInfo.SetValue(publisher, null);

            // Act
            await publisher.PublishMessageAsync(
                _normalExchange,
                _normalRouting,
                properties,
                body: Encoding.UTF8.GetBytes(StringGenerator.GeneratePrintableAscii()));

            // Assert
            var pendingMessages = GetPendingMessages(publisher);
            var droppedMessages = GetDroppedMessages(publisher);
            Assert.Empty(pendingMessages);
            Assert.Single(droppedMessages);

            CheckHeaders(droppedMessages.Values.First());
        }

        private async Task InitializePublisherAsync(TestPublisher publisher,
            AsyncEventHandler<BasicAckEventArgs>? acknowledgeEvent = null,
            AsyncEventHandler<BasicNackEventArgs>? notAcknowledgeEvent = null,
            AsyncEventHandler<BasicReturnEventArgs>? returnEvent = null)
        {
            var initializeAsyncMethodInfo = typeof(Publisher)
                .GetMethod("InitializeAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            await (Task)initializeAsyncMethodInfo.Invoke(publisher, [_collectionCluster.RabbitMqFixture.Connection, default])!;

            publisher.Channel!.BasicAcksAsync += acknowledgeEvent;
            publisher.Channel.BasicNacksAsync += notAcknowledgeEvent;
            publisher.Channel.BasicReturnAsync += returnEvent;
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

        private void CheckHeaders(Message message)
        {
            var headers = message.GetHeadersAsJsonString();
            Assert.NotNull(headers);

            int key1Value = JsonSerializer.Deserialize<int>((string)headers["Key1"]!);
            string key2Value = JsonSerializer.Deserialize<string>((string)headers["Key2"]!)!;
            bool key3Value = JsonSerializer.Deserialize<bool>((string)headers["Key3"]!);
            double key4Value = JsonSerializer.Deserialize<double>((string)headers["Key4"]!);
            int[] key5Value = JsonSerializer.Deserialize<int[]>((string)headers["Key5"]!)!;
            byte[] key6Value = JsonSerializer.Deserialize<byte[]>((string)headers["Key6"]!)!;
            TestClass key7Value = JsonSerializer.Deserialize<TestClass>((string)headers["Key7"]!)!;
            Dictionary<int, string> key8Value = JsonSerializer.Deserialize<Dictionary<int, string>>((string)headers["Key8"]!)!;
            string key9Value = JsonSerializer.Deserialize<string>((string)headers["Key9"]!)!;

            Assert.Equal((int)_testHeaders["Key1"]!, key1Value);
            Assert.Equal((string)_testHeaders["Key2"]!, key2Value);
            Assert.Equal((bool)_testHeaders["Key3"]!, key3Value);
            Assert.Equal((double)_testHeaders["Key4"]!, key4Value);
            Assert.True(((int[])_testHeaders["Key5"]!).SequenceEqual(key5Value), "Key5 header differs.");
            Assert.True(((byte[])_testHeaders["Key6"]!).SequenceEqual(key6Value), "Key6 header differs.");
            Assert.Equal(((TestClass)_testHeaders["Key7"]!).IntValue, key7Value.IntValue);
            Assert.Equal(((TestClass)_testHeaders["Key7"]!).StrValue, key7Value.StrValue);
            Assert.Equal(((Dictionary<int, string>)_testHeaders["Key8"]!)[1], key8Value[1]);
            Assert.Equal(((Dictionary<int, string>)_testHeaders["Key8"]!)[2], key8Value[2]);
            Assert.Equal((string)_testHeaders["Key9"]!, key9Value);
        }

        private class TestPublisher : Publisher
        {
            public TestPublisher() 
                : base(
                RabbitMqCollectionCluster.TestServiceName,
                RabbitMqCollectionCluster.TestPublisherName,
                maxRetryForMesages: 3)
            {
            }

            protected override async Task DeclareExchangesAsync(CancellationToken cancellationToken = default)
            {
                await Channel!.ExchangeDeclareAsync(
                    _normalExchange,
                    ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                await Channel.ExchangeDeclareAsync(
                    _rejectExchange,
                    ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);
                
                await Channel.ExchangeDeclareAsync(
                    _noQueueExchange,
                    ExchangeType.Direct,
                    durable: true,
                    autoDelete: false);

                // Declare queues for test
                await Channel.QueueDeclareAsync(
                    _normalQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false);
                await Channel.QueueBindAsync(
                    _normalQueue,
                    _normalExchange,
                    _normalRouting);

                await Channel.QueueDeclareAsync(
                    _rejectQueue,
                    durable: true,
                    exclusive: false,
                    autoDelete: false,
                    arguments: new Dictionary<string, object?>()
                    {
                        { "x-max-length", 0 },
                        { "x-overflow", "reject-publish" }
                    });
                await Channel.QueueBindAsync(
                    _rejectQueue,
                    _rejectExchange,
                    _rejectRouting);
            }
        }

        public class TestClass
        {
            public int IntValue { get; set; } = 5;
            public string StrValue { get; set; } = "StrValue";
        }
    }
}
