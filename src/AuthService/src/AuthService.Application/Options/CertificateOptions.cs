using Shared.AspNetCore.Helpers.Options;

namespace AuthService.Application.Options
{
    public class CertificateOptions : IOptions
    {
        public static string Key => "Certificate";

        public required string CABundlePath { get; set; }
        public required string ServerKeyFilePath { get; set; }
        public required string ServerCertificateFilePath { get; set; }
    }
}
