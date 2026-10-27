using RabbitMQ.Client;

namespace Shared.RabbitMq.Helpers.Structures
{
    /// <summary>
    /// Represents a message that is sent to RabbitMQ.
    /// </summary>
    public class Message
    {
        public string ServiceName { get; init; }
        public string PublisherName { get; init; }

        public string ExchangeName { get; init; }
        public string RoutingKey { get; init; }
        public BasicProperties Properties { get; init; }
        public byte[] Body { get; init; }

        internal ulong DeliveryTag { get; set; }
        internal int RetryCount { get; set; }

        public Message(
            string serviceName,
            string publisherName,
            string exchangeName,
            string routingKey,
            BasicProperties properties,
            byte[] body)
        {
            ServiceName = serviceName;
            PublisherName = publisherName;
            ExchangeName = exchangeName;
            RoutingKey = routingKey;
            Properties = properties;
            Body = body;
            DeliveryTag = 0;
            RetryCount = 0;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(
                ServiceName,
                PublisherName,
                ExchangeName,
                RoutingKey,
                Properties.CorrelationId!.GetHashCode());
        }
    }
}
