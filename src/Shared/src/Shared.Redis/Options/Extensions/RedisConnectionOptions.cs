using Shared.AspNetCore.Helpers.Tls.Options.Extensions;

namespace Shared.Redis.Options.Extensions
{
    public class RedisConnectionOptions
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string Password { get; set; }
        public required bool AbortOnConnectionFail { get; set; }
        public SslOptions? Ssl { get; set; }
    }
}
