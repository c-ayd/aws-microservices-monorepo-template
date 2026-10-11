using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Shared.AspNetCore.Helpers.Tls.Constants;

namespace Shared.AspNetCore.Helpers.Tls.Utilities
{
    public static partial class TlsUtility
    {
        public static bool ValidateClientCertificate(X509Certificate2? clientCertificate, X509Certificate2 caBundle)
        {
            if (clientCertificate == null)
                return false;

            // Check if the certificate is a client certificate
            if (!clientCertificate.Extensions.OfType<X509EnhancedKeyUsageExtension>()
                .Any(e => e.EnhancedKeyUsages
                    .Cast<Oid>()
                    .Any(o => o.Value == CertificateConstants.ClientCertificateOid)))
                return false;

            using var localChain = new X509Chain();
            localChain.ChainPolicy.CustomTrustStore.Add(caBundle);
            localChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            localChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;

            return localChain.Build(clientCertificate);
        }
    }
}
