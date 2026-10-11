using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Shared.AspNetCore.Helpers.Tls.Utilities
{
    public static partial class TlsUtility
    {
        public static X509Certificate2 LoadCertificateFromFile(string keyFilePath, string certificatePath)
        {
            // On Windows, the certificate's private key is loaded differently
            X509Certificate2 certificate;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var tempCert = X509Certificate2.CreateFromPemFile(certificatePath, keyFilePath);
                certificate = X509CertificateLoader.LoadPkcs12(tempCert.Export(X509ContentType.Pkcs12), "");
            }
            else
            {
                certificate = X509Certificate2.CreateFromPemFile(certificatePath, keyFilePath);
            }

            return certificate;
        }
    }
}
