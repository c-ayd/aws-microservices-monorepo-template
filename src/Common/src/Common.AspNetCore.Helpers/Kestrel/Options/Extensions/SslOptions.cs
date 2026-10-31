namespace Common.AspNetCore.Helpers.Kestrel.Options.Extensions
{
    public class SslOptions
    {
        public required bool Enabled { get; set; }
        public string? ServerName { get; set; }
    }
}
