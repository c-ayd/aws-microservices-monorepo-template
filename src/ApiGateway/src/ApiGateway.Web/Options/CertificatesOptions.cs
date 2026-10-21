using Shared.AspNetCore.Helpers.Options;

namespace ApiGateway.Web.Options
{
    public class CertificatesOptions : IOptions
    {
        public static string Key => "Certificate";

        public required string ServerKey { get; set; }
        public required string ServerCertificate { get; set; }
    }
}
