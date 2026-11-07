using System.Text;
using Lambda.SecretsRotations.Structures;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using Org.BouncyCastle.X509.Extension;

namespace Lambda.SecretsRotations.Utilities
{
    public static partial class Utility
    {
        /// <summary>
        /// Issues new certifiactes for a service.
        /// </summary>
        /// <param name="intermediateCertificate">Value of the certificate of intermediate CA</param>
        /// <param name="intermediatePrivateKey">Value of the private key of intermediate CA</param>
        /// <param name="intermediatePassword">Password for the private key of intermediate CA</param>
        /// <param name="mTls">Whether the client certificates should be issued</param>
        /// <param name="organization">Organization for the subject attribute</param>
        /// <param name="country">Country for the subject attribute</param>
        /// <param name="serviceName">Name for the subject attribute</param>
        /// <param name="dnsNames">DSN names of the service</param>
        /// <param name="startDate">When the certificate should become valid</param>
        /// <param name="validDays">How long the certificate should stay valid</param>
        /// <returns>Returns the generated certificates.</returns>
        public static IssuedCertificates IssueCertificates(
            string intermediateCertificate,
            string intermediatePrivateKey,
            string intermediatePassword,
            bool mTls,
            string organization,
            string country,
            string serviceName,
            List<string> dnsNames,
            DateTimeOffset startDate,
            int validDays)
        {
            IssueCertificates_LoadIntermediateCertificate(
                intermediateCertificate,
                intermediatePrivateKey,
                intermediatePassword,
                out var intermediateCertificateLoaded,
                out var intermediatePrivateKeyLoaded);

            var server = IssueCertificates_IssueServerCertificate(
                intermediateCertificateLoaded,
                intermediatePrivateKeyLoaded,
                organization,
                country,
                serviceName,
                dnsNames,
                startDate,
                validDays);

            if (mTls)
            {
                var client = IssueCertificates_IssueClientCertificate(
                    intermediateCertificateLoaded,
                    intermediatePrivateKeyLoaded,
                    organization,
                    country,
                    serviceName,
                    startDate,
                    validDays);

                return new IssuedCertificates(server, client);
            }

            return new IssuedCertificates(server, null);
        }

        private static void IssueCertificates_LoadIntermediateCertificate(
            string intermediateCertificate,
            string intermediatePrivateKey,
            string password,
            out X509Certificate certificate,
            out RsaPrivateCrtKeyParameters privateKey)
        {
            using var certificateReader = new StringReader(intermediateCertificate);
            using var certificatePemReader = new PemReader(certificateReader);
            certificate = (X509Certificate)certificatePemReader.ReadObject();

            using var keyReader = new StringReader(intermediatePrivateKey);
            using var keyPemReader = new PemReader(keyReader, new PasswordFinder(password));
            privateKey = (RsaPrivateCrtKeyParameters)keyPemReader.ReadObject();
        }

        private class PasswordFinder : IPasswordFinder
        {
            private readonly string _password;
            public PasswordFinder(string password) => _password = password;
            public char[] GetPassword() => _password.ToCharArray();
        }

        private static CertificateValues IssueCertificates_IssueServerCertificate(
            X509Certificate intermediateCertificate,
            AsymmetricKeyParameter intermediatePrivateKey,
            string organization,
            string country,
            string serviceName,
            List<string> dnsNames,
            DateTimeOffset startDate,
            int validDays)
        {
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(65537), new SecureRandom(), strength: 2048, certainty: 24));
            var keys = keyGenerator.GenerateKeyPair();

            var subject = new X509Name(
                new List<DerObjectIdentifier> { X509Name.O, X509Name.C, X509Name.CN },
                new List<string> { organization, country, serviceName });

            var serial = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(2).Pow(157), new SecureRandom());
            var san = new GeneralNames(dnsNames.Select(d => new GeneralName(GeneralName.DnsName, d)).ToArray());
            var extValue = intermediateCertificate.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);
            var issuerSki = SubjectKeyIdentifier.GetInstance(Asn1Object.FromByteArray(extValue.GetOctets()));

            var generator = new X509V3CertificateGenerator();
            generator.SetIssuerDN(intermediateCertificate.SubjectDN);
            generator.SetSerialNumber(serial);
            generator.SetSubjectDN(subject);
            generator.SetNotBefore(startDate.UtcDateTime);
            generator.SetNotAfter(startDate.AddDays(validDays).UtcDateTime);
            generator.SetPublicKey(keys.Public);
            generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
            generator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));
            generator.AddExtension(X509Extensions.ExtendedKeyUsage, true, new ExtendedKeyUsage(KeyPurposeID.id_kp_serverAuth));
            generator.AddExtension(X509Extensions.SubjectAlternativeName, false, san);
            generator.AddExtension(X509Extensions.SubjectKeyIdentifier, false, X509ExtensionUtilities.CreateSubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keys.Public)));
            generator.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, new AuthorityKeyIdentifier(issuerSki.GetKeyIdentifier()));

            var certificate = generator.Generate(new Asn1SignatureFactory("SHA256withRSA", intermediatePrivateKey));

            return new CertificateValues(
                Certificate: IssueCertificates_ExportCertificateBundle(certificate, intermediateCertificate),
                PrivateKey: IssueCertificates_ExportPrivateKeyPem(keys)
            );
        }

        private static CertificateValues IssueCertificates_IssueClientCertificate(
            X509Certificate intermediateCertificate,
            AsymmetricKeyParameter intermediatePrivateKey,
            string organization,
            string country,
            string serviceName,
            DateTimeOffset startDate,
            int validDays)
        {
            var keyGenerator = new RsaKeyPairGenerator();
            keyGenerator.Init(new RsaKeyGenerationParameters(BigInteger.ValueOf(65537), new SecureRandom(), strength: 2048, certainty: 24));
            var keys = keyGenerator.GenerateKeyPair();

            var subject = new X509Name(
                new List<DerObjectIdentifier> { X509Name.O, X509Name.C, X509Name.CN },
                new List<string> { organization, country, serviceName });

            var serial = BigIntegers.CreateRandomInRange(BigInteger.One, BigInteger.ValueOf(2).Pow(157), new SecureRandom());
            var san = new GeneralNames(new GeneralName(GeneralName.Rfc822Name, $"{serviceName}@{organization}"));
            var extValue = intermediateCertificate.GetExtensionValue(X509Extensions.SubjectKeyIdentifier);
            var issuerSki = SubjectKeyIdentifier.GetInstance(Asn1Object.FromByteArray(extValue.GetOctets()));

            var generator = new X509V3CertificateGenerator();
            generator.SetIssuerDN(intermediateCertificate.SubjectDN);
            generator.SetSerialNumber(serial);
            generator.SetSubjectDN(subject);
            generator.SetNotBefore(startDate.UtcDateTime);
            generator.SetNotAfter(startDate.AddDays(validDays).UtcDateTime);
            generator.SetPublicKey(keys.Public);
            generator.AddExtension(X509Extensions.BasicConstraints, true, new BasicConstraints(false));
            generator.AddExtension(X509Extensions.KeyUsage, true, new KeyUsage(KeyUsage.DigitalSignature | KeyUsage.KeyEncipherment));
            generator.AddExtension(X509Extensions.ExtendedKeyUsage, true, new ExtendedKeyUsage(KeyPurposeID.id_kp_clientAuth));
            generator.AddExtension(X509Extensions.SubjectAlternativeName, false, san);
            generator.AddExtension(X509Extensions.SubjectKeyIdentifier, false, X509ExtensionUtilities.CreateSubjectKeyIdentifier(SubjectPublicKeyInfoFactory.CreateSubjectPublicKeyInfo(keys.Public)));
            generator.AddExtension(X509Extensions.AuthorityKeyIdentifier, false, new AuthorityKeyIdentifier(issuerSki.GetKeyIdentifier()));

            var certificate = generator.Generate(new Asn1SignatureFactory("SHA256withRSA", intermediatePrivateKey));

            return new CertificateValues(
                Certificate: IssueCertificates_ExportCertificateBundle(certificate, intermediateCertificate),
                PrivateKey: IssueCertificates_ExportPrivateKeyPem(keys)
            );
        }

        private static string IssueCertificates_ExportCertificateBundle(X509Certificate leaf, X509Certificate intermediate)
        {
            var builder = new StringBuilder();
            builder.Append(IssueCertificates_ExportSingleCertificate(leaf));
            builder.Append(IssueCertificates_ExportSingleCertificate(intermediate));

            return builder.ToString();
        }

        private static string IssueCertificates_ExportSingleCertificate(X509Certificate certificate)
        {
            var certificateBase64 = Convert.ToBase64String(certificate.GetEncoded());
            var builder = new StringBuilder();

            builder.AppendLine("-----BEGIN CERTIFICATE-----");
            for (int i = 0; i < certificateBase64.Length; i += 64)
            {
                int end = Math.Min(i + 64, certificateBase64.Length);
                builder.AppendLine(certificateBase64.Substring(i, end - i));
            }
            builder.AppendLine("-----END CERTIFICATE-----");

            return builder.ToString();
        }

        private static string IssueCertificates_ExportPrivateKeyPem(AsymmetricCipherKeyPair keyPair)
        {
            var privateKeyInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo(keyPair.Private);
            var derBytes = privateKeyInfo.GetDerEncoded();
            var base64 = Convert.ToBase64String(derBytes);
            var builder = new StringBuilder();

            builder.AppendLine("-----BEGIN PRIVATE KEY-----");
            for (int i = 0; i < base64.Length; i += 64)
            {
                int end = Math.Min(i + 64, base64.Length);
                builder.AppendLine(base64.Substring(i, end - i));
            }
            builder.AppendLine("-----END PRIVATE KEY-----");

            return builder.ToString();
        }
    }
}
