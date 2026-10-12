using Shared.AspNetCore.Helpers;

namespace ApiGateway.Web.Options
{
    public class CertificateOptions : IOptions
    {
        public static string Key => "Certificate";

        public required string CABundlePath { get; set; }
        public required string ServerKeyFilePath { get; set; }
        public required string ServerCertificateFilePath { get; set; }
    }
}
