namespace Shared.AspNetCore.Helpers.Tls.Options.Extensions
{
    public class SslOptions
    {
        public required string CABundlePath { get; set; }
        public string? ServerName { get; set; }
    }
}
