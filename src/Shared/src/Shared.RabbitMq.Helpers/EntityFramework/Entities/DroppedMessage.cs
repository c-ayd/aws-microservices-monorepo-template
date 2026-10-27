#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using System.Text.Json;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.JsonConverters;
using Shared.RabbitMq.Helpers.Structures;
using Shared.RabbitMq.Helpers.Utilities;

namespace Shared.RabbitMq.Helpers.EntityFramework.Entities
{
    /// <summary>
    /// Holds necessary information about a dropped message for Entity Framework Core.
    /// </summary>
    public class DroppedMessage
    {
        public Guid Id { get; init; }

        public string ServiceName { get; set; }
        public string PublisherName { get; set; }

        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
        public string Properties { get; set; }
        public byte[] BodyEncrypted { get; set; }

        // Relationships
        public Guid WrappedDataKeyId { get; set; }
        public WrappedDataKey WrappedDataKey { get; set; }

        private BasicProperties? _basicProperties = null;

        // Reserved for EF Core
        private DroppedMessage()
        {
        }

        public DroppedMessage(
            Guid wrappedDataKeyId,
            Message message,
            byte[] bodyEncrypted)
        {
            WrappedDataKeyId = wrappedDataKeyId;

            Id = Guid.CreateVersion7();

            ServiceName = message.ServiceName;
            PublisherName = message.PublisherName;

            ExchangeName = message.ExchangeName;
            RoutingKey = message.RoutingKey;
            BodyEncrypted = bodyEncrypted;

            // The message might be returned from RabbitMQ, which causes headers to be byte arrays.
            // Therefore, the headers is converted back to JSON strings before saved to the DB.
            message.Properties.Headers = message.GetHeadersAsJsonString();
            Properties = JsonSerializer.Serialize(message.Properties, new JsonSerializerOptions()
            {
                Converters = { new AmqpTimestampConverter(), new HeadersConverter() }
            });
        }

        public BasicProperties GetBasicProperties()
        {
            if (_basicProperties == null)
            {
                _basicProperties = Utility.ConvertStringToBasicProperties(Properties);
            }

            return _basicProperties;
        }
    }
}
