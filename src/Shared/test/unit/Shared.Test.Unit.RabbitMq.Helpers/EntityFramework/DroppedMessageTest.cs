using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.EntityFramework.Entities;

namespace Shared.Test.Unit.RabbitMq.Helpers.EntityFramework
{
    public class DroppedMessageTest
    {
        private const string _serviceName = "TestService";
        private const string _publisherName = "TestPublisher";

        private const string _exchangeName = "shared.rabbitmq.helpers.droppedmessage.exchange";
        private const string _routingKey = "shared.rabbitmq.helpers.droppedmessage.routing";

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
        private readonly BasicProperties _testProperties = new BasicProperties()
        {
            DeliveryMode = DeliveryModes.Persistent,
            CorrelationId = Guid.NewGuid().ToString(),
            AppId = Guid.NewGuid().ToString(),
            MessageId = Guid.NewGuid().ToString(),
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WhenPropertiesAreJsonStrings_ShouldConvertVariablesCorrectly(bool addHeaders)
        {
            // Arrange
            var wrappedDataKey = new WrappedDataKey(Encoding.UTF8.GetBytes("Test key"));
            var body = Encoding.UTF8.GetBytes("Test message!");
            var properties = new BasicProperties(_testProperties);

            if (addHeaders)
            {
                properties.Headers = new Dictionary<string, object?>(_testHeaders);
                foreach (var header in properties.Headers!)
                {
                    properties.Headers[header.Key] = JsonSerializer.Serialize(header.Value);
                }
            }

            // Act
            var droppedMessage = new DroppedMessage(
                wrappedDataKey.Id,
                _serviceName,
                _publisherName,
                _exchangeName,
                _routingKey,
                properties,
                body);

            // Assert
            Assert.Equal(_exchangeName, droppedMessage.ExchangeName);
            Assert.Equal(_routingKey, droppedMessage.RoutingKey);
            Assert.Equal(body, droppedMessage.BodyEncrypted);

            CheckBasicProperties(droppedMessage.GetBasicProperties());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Constructor_WhenPropertiesAreByteArrays_ShouldConvertVariablesCorrectly(bool addHeaders)
        {
            // Arrange
            var wrappedDataKey = new WrappedDataKey(Encoding.UTF8.GetBytes("Test key"));
            var body = Encoding.UTF8.GetBytes("Test message!");
            var properties = new BasicProperties(_testProperties);

            if (addHeaders)
            {
                properties.Headers = new Dictionary<string, object?>(_testHeaders);
                foreach (var header in properties.Headers!)
                {
                    properties.Headers[header.Key] = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(header.Value));
                }
            }

            // Act
            var droppedMessage = new DroppedMessage(
                wrappedDataKey.Id,
                _serviceName,
                _publisherName,
                _exchangeName,
                _routingKey,
                properties,
                body);

            // Assert
            Assert.Equal(_exchangeName, droppedMessage.ExchangeName);
            Assert.Equal(_routingKey, droppedMessage.RoutingKey);
            Assert.Equal(body, droppedMessage.BodyEncrypted);

            CheckBasicProperties(droppedMessage.GetBasicProperties());
        }
        
        private void CheckBasicProperties(BasicProperties properties)
        {
            Assert.Equal(_testProperties.DeliveryMode, properties.DeliveryMode);
            Assert.Equal(_testProperties.CorrelationId, properties.CorrelationId);
            Assert.Equal(_testProperties.AppId, properties.AppId);
            Assert.Equal(_testProperties.MessageId, properties.MessageId);
            Assert.Equal(_testProperties.Timestamp, properties.Timestamp);

            if (properties.Headers == null)
                return;

            var key1Value = JsonSerializer.Deserialize<int>((string)properties.Headers!["Key1"]!);
            var key2Value = JsonSerializer.Deserialize<string>((string)properties.Headers!["Key2"]!)!;
            var key3Value = JsonSerializer.Deserialize<bool>((string)properties.Headers!["Key3"]!);
            var key4Value = JsonSerializer.Deserialize<double>((string)properties.Headers!["Key4"]!);
            var key5Value = JsonSerializer.Deserialize<int[]>((string)properties.Headers!["Key5"]!)!;
            var key6Value = JsonSerializer.Deserialize<byte[]>((string)properties.Headers!["Key6"]!)!;
            var key7Value = JsonSerializer.Deserialize<TestClass>((string)properties.Headers!["Key7"]!)!;
            var key8Value = JsonSerializer.Deserialize<Dictionary<int, string>>((string)properties.Headers!["Key8"]!)!;
            var key9Value = JsonSerializer.Deserialize<string>((string)properties.Headers!["Key9"]!)!;

            Assert.Equal((int)_testHeaders["Key1"]!, key1Value);
            Assert.Equal((string)_testHeaders["Key2"]!, key2Value);
            Assert.Equal((bool)_testHeaders["Key3"]!, key3Value);
            Assert.Equal((double)_testHeaders["Key4"]!, key4Value);
            Assert.Equal((int[])_testHeaders["Key5"]!, key5Value);
            Assert.Equal((byte[])_testHeaders["Key6"]!, key6Value);
            Assert.Equal(((TestClass)_testHeaders["Key7"]!).IntValue, key7Value.IntValue);
            Assert.Equal(((TestClass)_testHeaders["Key7"]!).StrValue, key7Value.StrValue);
            Assert.Equal(((Dictionary<int, string>)_testHeaders["Key8"]!)[1], key8Value[1]);
            Assert.Equal(((Dictionary<int, string>)_testHeaders["Key8"]!)[2], key8Value[2]);
            Assert.Equal((string)_testHeaders["Key9"]!, key9Value);
        }

        public class TestClass
        {
            public int IntValue { get; set; } = 5;
            public string StrValue { get; set; } = "StrValue";
        }
    }
}
