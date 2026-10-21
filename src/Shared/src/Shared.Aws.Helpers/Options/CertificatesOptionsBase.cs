using Shared.AspNetCore.Helpers.Options;

namespace Shared.Aws.Helpers.Options
{
    public abstract class CertificatesOptionsBase : IOptions
    {
        public static string Key => "Certificate";

        public required string ServerKey { get; set; }
        public required string ServerCertificate { get; set; }
    }
}
