using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace Common.AspNetCore.Helpers.Kestrel.Utilities
{
    public static partial class TlsUtility
    {
        /// <summary>
        /// Loads a certificate based on given values.
        /// </summary>
        /// <param name="certificateValue">Value of the certificate</param>
        /// <param name="privateKeyValue">Value of the private key for the certificate</param>
        /// <returns>Returns the certificate.</returns>
        public static X509Certificate2 LoadCertificate(string certificateValue, string privateKeyValue)
        {
            // On Windows, the certificate's private key is loaded differently
            X509Certificate2 resultCertificate;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                using var tempCert = X509Certificate2.CreateFromPem(certificateValue, privateKeyValue);
                resultCertificate = X509CertificateLoader.LoadPkcs12(tempCert.Export(X509ContentType.Pkcs12), "");
            }
            else
            {
                resultCertificate = X509Certificate2.CreateFromPem(certificateValue, privateKeyValue);
            }

            return resultCertificate;
        }

        /// <summary>
        /// Loads a certificate chain based on a given certificate value.
        /// </summary>
        /// <param name="certificateValue">Value of the certificate</param>
        /// <param name="skipFirstInChain">Whether to skip the first certificate in the chain</param>
        /// <returns>Returns the certificate chain.</returns>
        public static X509Certificate2Collection LoadCertificateChain(string certificateValue, bool skipFirstInChain = false)
        {
            var certificateChain = new X509Certificate2Collection();
            certificateChain.ImportFromPem(certificateValue);

            if (skipFirstInChain)
            {
                certificateChain.Remove(certificateChain[0]);
            }

            return certificateChain;
        }
    }
}
