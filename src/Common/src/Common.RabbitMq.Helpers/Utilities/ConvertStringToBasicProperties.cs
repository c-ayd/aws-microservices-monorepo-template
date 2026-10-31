using System.Text.Json;
using RabbitMQ.Client;
using Common.RabbitMq.Helpers.JsonConverters;

namespace Common.RabbitMq.Helpers.Utilities
{
    public static partial class Utility
    {
        /// <summary>
        /// Converts the JSON string to <see cref="RabbitMQ.Client.BasicProperties"/>.
        /// </summary>
        /// <returns>Returns the converted basic properties.</returns>
        public static BasicProperties ConvertStringToBasicProperties(string properties)
        {
            return JsonSerializer.Deserialize<BasicProperties>(properties, new JsonSerializerOptions()
            {
                Converters = { new AmqpTimestampConverter(), new HeadersConverter() }
            })!;
        }
    }
}
