using Shared.AspNetCore.Helpers.Options;

namespace Shared.Aws.Helpers.Options
{
    public abstract class CertificatesOptionsBase : IOptions
    {
        public static string Key => "Certificate";

        public required string ServerCertificate { get; set; }
        public required string ServerPrivateKey { get; set; }
    }
}
