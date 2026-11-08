namespace Common.RabbitMq.Helpers.Options.Extensions
{
    public class RabbitMqConnectionOptions
    {
        public required string Host { get; set; }
        public required int Port { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
        public required bool SslEnabled { get; set; }
    }
}
