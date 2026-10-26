using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Shared.AspNetCore.Helpers.Kestrel.Utilities
{
    public static partial class TlsUtility
    {
        public static X509Certificate2 LoadCertificate(string certificate, string privateKey)
        {
            // On Windows, the certificate's private key is loaded differently
            X509Certificate2 resultCertificate;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var tempCert = X509Certificate2.CreateFromPem(certificate, privateKey);
                resultCertificate = X509CertificateLoader.LoadPkcs12(tempCert.Export(X509ContentType.Pkcs12), "");
            }
            else
            {
                resultCertificate = X509Certificate2.CreateFromPem(certificate, privateKey);
            }

            return resultCertificate;
        }

        public static X509Certificate2Collection LoadCertificateChain(string certificate, bool skipFirstInChain = false)
        {
            var certificateChain = new X509Certificate2Collection();
            certificateChain.ImportFromPem(certificate);

            if (skipFirstInChain)
            {
                certificateChain.Remove(certificateChain[0]);
            }

            return certificateChain;
        }
    }
}
