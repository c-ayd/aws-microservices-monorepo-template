using System.Text.Json;
using System.Text.Json.Serialization;
using RabbitMQ.Client;

namespace Common.RabbitMq.Helpers.JsonConverters
{
    internal class AmqpTimestampConverter : JsonConverter<AmqpTimestamp>
    {
        public override void Write(Utf8JsonWriter writer, AmqpTimestamp value, JsonSerializerOptions options)
        {
            writer.WriteNumberValue(value.UnixTime);
        }

        public override AmqpTimestamp Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            return new AmqpTimestamp(reader.GetInt64());
        }
    }
}
