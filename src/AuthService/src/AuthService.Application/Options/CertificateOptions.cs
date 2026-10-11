using Shared.AspNetCore.Helpers;

namespace AuthService.Application.Options
{
    public class CertificateOptions : IOptions
    {
        public static string Key => "Certificate";

        public required string CABundlePath { get; set; }
        public required string ServerKeyFilePath { get; set; }
        public required string ServerCertificatePath { get; set; }
    }
}
