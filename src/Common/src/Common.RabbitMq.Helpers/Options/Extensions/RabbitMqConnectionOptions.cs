using System.Text.Json.Serialization;
using Common.AspNetCore.Helpers.Kestrel.Options.Extensions;

namespace Common.RabbitMq.Helpers.Options.Extensions
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class RabbitMqConnectionOptions
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required SslOptions Ssl { get; set; }
    }
}
