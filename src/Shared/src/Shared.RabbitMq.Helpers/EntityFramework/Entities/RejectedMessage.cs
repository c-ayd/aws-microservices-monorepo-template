#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using System.Text.Json;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.JsonConverters;
using Shared.RabbitMq.Helpers.Utilities;

namespace Shared.RabbitMq.Helpers.EntityFramework.Entities
{
    /// <summary>
    /// Holds necessary information about a rejected message for Entity Framework Core.
    /// </summary>
    public class RejectedMessage
    {
        public Guid Id { get; init; }

        public string ExchangeName { get; set; }
        public string RoutingKey { get; set; }
        public string Properties { get; set; }
        public byte[] BodyEncrypted { get; set; }

        // Relationships
        public Guid WrappedDataKeyId { get; set; }
        public WrappedDataKey WrappedDataKey { get; set; }

        private BasicProperties? _basicProperties = null;

        // Reserved for EF Core
        private RejectedMessage()
        {
        }

        public RejectedMessage(
            Guid wrappedDataKeyId,
            string exchangeName,
            string routingKey,
            BasicProperties properties,
            byte[] bodyEncrypted)
        {
            WrappedDataKeyId = wrappedDataKeyId;

            Id = Guid.CreateVersion7();

            ExchangeName = exchangeName;
            RoutingKey = routingKey;
            Properties = JsonSerializer.Serialize(properties, new JsonSerializerOptions()
            {
                Converters = { new AmqpTimestampConverter(), new HeadersConverter() }
            });
            BodyEncrypted = bodyEncrypted;
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
