using Common.AspNetCore.Helpers.Kestrel.Options.Extensions;

namespace Common.Redis.Helpers.Options.Extensions
{
    public class RedisConnectionOptions
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string Password { get; set; }
        public required bool AbortOnConnectionFail { get; set; }
        public required SslOptions Ssl { get; set; }
    }
}
