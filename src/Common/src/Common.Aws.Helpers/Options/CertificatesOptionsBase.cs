using Common.AspNetCore.Helpers.Options;

namespace Common.Aws.Helpers.Options
{
    public abstract class CertificatesOptionsBase : IOptions
    {
        public static string Key => "Certificate";

        public required string ServerCertificate { get; set; }
        public required string ServerPrivateKey { get; set; }
    }
}
