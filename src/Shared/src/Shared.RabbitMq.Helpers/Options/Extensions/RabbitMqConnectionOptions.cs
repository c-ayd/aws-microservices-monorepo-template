using Shared.AspNetCore.Helpers.Kestrel.Options.Extensions;

namespace Shared.RabbitMq.Helpers.Options.Extensions
{
    public class RabbitMqConnectionOptions
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required SslOptions Ssl { get; set; }
    }
}
