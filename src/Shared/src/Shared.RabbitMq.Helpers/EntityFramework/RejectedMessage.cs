#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

using System.Text.Json;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.JsonConverters;

namespace Shared.RabbitMq.Helpers.EntityFramework
{
    /// <summary>
    /// Holds necessary information about a rejected message for Entity Framework Core.
    /// </summary>
    public class RejectedMessage
    {
        public Guid Id { get; init; }

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
        private RejectedMessage()
        {
        }

        public RejectedMessage(
            Guid wrappedDataKeyId,
            string publisherName,
            string exchangeName,
            string routingKey,
            BasicProperties properties,
            byte[] bodyEncrypted)
        {
            WrappedDataKeyId = wrappedDataKeyId;

            Id = Guid.CreateVersion7();

            PublisherName = publisherName;
            ExchangeName = exchangeName;
            RoutingKey = routingKey;
            Properties = JsonSerializer.Serialize(properties, new JsonSerializerOptions()
            {
                Converters = { new AmqpTimestampConverter(), new HeadersConverter() }
            });
            BodyEncrypted = bodyEncrypted;
        }

        /// <summary>
        /// Converts the <see cref="RejectedMessage.Properties"/> property to <see cref="RabbitMQ.Client.BasicProperties"/>.
        /// </summary>
        /// <returns>Returns the converted property values.</returns>
        public BasicProperties GetBasicProperties()
        {
            if (_basicProperties == null)
            {
                _basicProperties = JsonSerializer.Deserialize<BasicProperties>(Properties, new JsonSerializerOptions()
                {
                    Converters = { new AmqpTimestampConverter(), new HeadersConverter() }
                })!;
            }

            return _basicProperties;
        }
    }
}
