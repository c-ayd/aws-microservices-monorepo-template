using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Shared.AspNetCore.Helpers.Kestrel.Utilities
{
    public static partial class TlsUtility
    {
        public static X509Certificate2 LoadCertificateFromFile(string keyFilePath, string certificateFilePath)
        {
            // On Windows, the certificate's private key is loaded differently
            X509Certificate2 certificate;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var tempCert = X509Certificate2.CreateFromPemFile(certificateFilePath, keyFilePath);
                certificate = X509CertificateLoader.LoadPkcs12(tempCert.Export(X509ContentType.Pkcs12), "");
            }
            else
            {
                certificate = X509Certificate2.CreateFromPemFile(certificateFilePath, keyFilePath);
            }

            return certificate;
        }

        public static X509Certificate2Collection LoadCertificateChainFromFile(string certificateFilePath, bool skipFirstInChain = false)
        {
            var certificateChain = new X509Certificate2Collection();
            certificateChain.ImportFromPemFile(certificateFilePath);

            if (skipFirstInChain)
            {
                certificateChain.Remove(certificateChain[0]);
            }

            return certificateChain;
        }
    }
}
