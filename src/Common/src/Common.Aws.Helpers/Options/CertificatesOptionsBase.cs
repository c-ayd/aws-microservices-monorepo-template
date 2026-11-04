using Common.AspNetCore.Helpers.Options;

namespace Common.Aws.Helpers.Options
{
    public class CertificatesOptionsBase : IOptions
    {
        public static string Key => "Certificates";

        public required string ServerCertificate { get; set; }
        public required string ServerPrivateKey { get; set; }
    }
}
