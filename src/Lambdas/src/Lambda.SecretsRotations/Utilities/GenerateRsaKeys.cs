using System.Security.Cryptography;

namespace Lambda.SecretsRotations.Utilities
{
    public static partial class Utility
    {
        /// <summary>
        /// Generates a new RSA key pair.
        /// </summary>
        /// <param name="publicKey">Public key of the generated pair</param>
        /// <param name="privateKey">Private key of the generated pair</param>
        public static void GenerateRsaKeys(out string publicKey, out string privateKey)
        {
            using var rsa = RSA.Create(keySizeInBits: 2048);
            publicKey = rsa.ExportSubjectPublicKeyInfoPem();
            privateKey = rsa.ExportPkcs8PrivateKeyPem();
        }
    }
}
