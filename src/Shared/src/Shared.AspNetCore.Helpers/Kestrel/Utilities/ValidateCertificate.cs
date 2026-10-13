using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shared.AspNetCore.Helpers.Kestrel.Constants;

namespace Shared.AspNetCore.Helpers.Kestrel.Utilities
{
    public static partial class TlsUtility
    {
        public static bool ValidateServerCertificate(
            X509Certificate? certificate,
            X509Chain? chain,
            X509Certificate2Collection caBundle,
            SslPolicyErrors sslPolicyErrors)
        {
            if (certificate == null ||
                (sslPolicyErrors & SslPolicyErrors.RemoteCertificateNotAvailable) != 0 ||
                (sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                return false;

            var cert2 = (X509Certificate2)certificate;

            // Check if the certificate is a server certificate
            if (!cert2.Extensions.OfType<X509EnhancedKeyUsageExtension>()
                .Any(e => e.EnhancedKeyUsages
                    .Cast<Oid>()
                    .Any(o => o.Value == CertificateConstants.ServerCertificateOid)))
                return false;

            using var localChain = new X509Chain();
            localChain.ChainPolicy.CustomTrustStore.AddRange(caBundle);
            localChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            localChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            if (chain != null)
            {
                foreach (var intermediate in chain.ChainElements.Cast<X509ChainElement>())
                {
                    localChain.ChainPolicy.ExtraStore.Add(intermediate.Certificate);
                }
            }

            return localChain.Build(cert2);
        }
    }
}
