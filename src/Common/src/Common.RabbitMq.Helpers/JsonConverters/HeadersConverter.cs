using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Common.RabbitMq.Helpers.JsonConverters
{
    internal class HeadersConverter : JsonConverter<IDictionary<string, object?>>
    {
        public override void Write(Utf8JsonWriter writer, IDictionary<string, object?> value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();

            foreach (var item in value)
            {
                writer.WritePropertyName(item.Key);

                if (item.Value == null)
                {
                    writer.WriteStringValue((string?)null);
                }
                else
                {
                    if (item.Value.GetType() == typeof(string))
                    {
                        // When a message is never returned from RabbitMQ, the headers stay as strings
                        writer.WriteStringValue((string)item.Value);
                    }
                    else if (item.Value.GetType() == typeof(byte[]))
                    {
                        // When a message returns from RabbitMQ, the headers become byte arrays
                        writer.WriteStringValue(Encoding.UTF8.GetString((byte[])item.Value));
                    }
                    else
                    {
                        // This should never happen, but as a safeguard, the value is converted to a string
                        writer.WriteStringValue(item.Value.ToString());
                    }
                }
            }

            writer.WriteEndObject();
        }

        public override IDictionary<string, object?>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            var result = new Dictionary<string, object?>();

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                var key = reader.GetString();
                reader.Read();
                result[key!] = reader.GetString();
            }

            return result;
        }
    }
}
