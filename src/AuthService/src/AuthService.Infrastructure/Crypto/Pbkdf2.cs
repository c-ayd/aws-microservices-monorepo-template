using System.Security.Cryptography;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Dtos.Crypto;

namespace AuthService.Infrastructure.Crypto
{
    public class Pbkdf2 : IPasswordHasher
    {
        public string Hash(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentException("The password cannot be null or empty.", nameof(password));

            var hashVersion = _hashVersions[_currentVersion];

            // Hashed password bytes are: {Version}{Salt}{Key}
            // Version bytes are 8 bits (1 byte)
            Span<byte> bytes = new byte[1 + hashVersion.SaltSize + hashVersion.KeySize];
            var saltBytes = bytes.Slice(1, hashVersion.SaltSize);
            var keyBytes = bytes.Slice(1 + hashVersion.SaltSize, hashVersion.KeySize);
            
            // Fill the bytes
            bytes[0] = _currentVersion;
            RandomNumberGenerator.Fill(saltBytes);
            Rfc2898DeriveBytes.Pbkdf2(password, saltBytes, keyBytes, hashVersion.Iterations, hashVersion.Algorithm);

            return Convert.ToBase64String(bytes);
        }

        public EPasswordVerificationResult Verify(string passwordHashed, string passwordPlain, out byte version)
        {
            if (string.IsNullOrEmpty(passwordHashed))
                throw new ArgumentException("The hashed password cannot be null or empty.", nameof(passwordHashed));
            if (string.IsNullOrEmpty(passwordPlain))
                throw new ArgumentException("The plain password cannot be null or empty.", nameof(passwordPlain));

            Span<byte> bytes = Convert.FromBase64String(passwordHashed).AsSpan();

            // Get version of the hashed password
            version = bytes[0];
            if (!_hashVersions.TryGetValue(version, out var hashVersion))
                return EPasswordVerificationResult.VersionNotFound;

            // Hashed password bytes are: {Version}{Salt}{Key}
            // Version bytes are 8 bits (1 byte)
            var expectedNumberOfBytes = 1 + hashVersion.SaltSize + hashVersion.KeySize;
            if (bytes.Length != expectedNumberOfBytes)
                return EPasswordVerificationResult.LengthMismatch;

            // Hash the plain password by using salt bytes of the hashed password
            // to compare the new key bytes with the key bytes of the hashed password
            var saltBytes = bytes.Slice(1, hashVersion.SaltSize);
            var expectedKeyBytes = bytes.Slice(1 + hashVersion.SaltSize, hashVersion.KeySize);
            Span<byte> keyBytes = new byte[hashVersion.KeySize];
            Rfc2898DeriveBytes.Pbkdf2(passwordPlain, saltBytes, keyBytes, hashVersion.Iterations, hashVersion.Algorithm);

            // To prevent timing attack, the comparison is made over CryptographicOperations
            if (!CryptographicOperations.FixedTimeEquals(expectedKeyBytes, keyBytes))
                return EPasswordVerificationResult.Fail;

            return version == _currentVersion ?
                EPasswordVerificationResult.Success :
                EPasswordVerificationResult.SuccessRehashNeeded;
        }

        private record HashVersion(
            HashAlgorithmName Algorithm,
            int SaltSize,
            int KeySize,
            int Iterations
        );

        private const byte _currentVersion = 1;
        private readonly Dictionary<byte, HashVersion> _hashVersions = new Dictionary<byte, HashVersion>()
        {
            { 1, new HashVersion(Algorithm: HashAlgorithmName.SHA256, SaltSize: 256 / 8, KeySize: 256 / 8, Iterations: 600_000) }
        };
    }
}
