using System.Text.Json.Serialization;
using Common.AspNetCore.Helpers.Kestrel.Options.Extensions;

namespace Common.PostgreSql.Helpers.Options.Extensions
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public class PostgreSqlConnectionOptions
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string Database { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required SslOptions Ssl { get; set; }
        public RetryMechanismOptions? RetryMechanism { get; set; }

        public class RetryMechanismOptions
        {
            public required int MaxRetry { get; set; }
            public required int MaxDelayInSeconds { get; set; }
        }
    }
}
