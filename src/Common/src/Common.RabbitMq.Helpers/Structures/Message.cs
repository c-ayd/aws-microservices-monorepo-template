using System.Text;
using RabbitMQ.Client;

namespace Common.RabbitMq.Helpers.Structures
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
        public bool IsReturned { get; init; }
        public byte[] Body { get; init; }

        internal ulong DeliveryTag { get; set; }
        internal int RetryCount { get; set; }

        public Message(
            string serviceName,
            string publisherName,
            string exchangeName,
            string routingKey,
            BasicProperties properties,
            bool isReturned,
            byte[] body)
        {
            ServiceName = serviceName;
            PublisherName = publisherName;
            ExchangeName = exchangeName;
            RoutingKey = routingKey;
            Properties = properties;
            IsReturned = isReturned;
            Body = body;
            DeliveryTag = 0;
            RetryCount = 0;
        }

        public IDictionary<string, object?>? GetHeadersAsJsonString()
        {
            if (Properties.Headers == null)
                return null;

            if (!IsReturned)
            {
                return Properties.Headers;
            }

            var headers = new Dictionary<string, object?>();
            foreach (var header in Properties.Headers)
            {
                headers.Add(header.Key, Encoding.UTF8.GetString((byte[])header.Value!));
            }

            return headers;
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
