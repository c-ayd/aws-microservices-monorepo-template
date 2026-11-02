using System.Security.Cryptography;
using System.Text;
using Common.Crypto.Exceptions;
using Common.Crypto.Structures;
using Microsoft.AspNetCore.WebUtilities;

namespace Common.Crypto
{
    /// <summary>
    /// Hashes values and verifies hashed and plain values.
    /// </summary>
    public static class ValueHasher
    {
        /// <summary>
        /// Hashes a given value.
        /// </summary>
        /// <param name="value">Value to hash</param>
        /// <param name="version">Version of the hash options</param>
        /// <param name="options">Delegate that returns hashing options based on the given version</param>
        /// <returns>Returns the hashed value.</returns>
        public static string Hash(string value, byte version, Func<byte, HashOptions> options)
        {
            return Hash(value, version, options, out _);
        }

        /// <summary>
        /// Hashes a given value.
        /// </summary>
        /// <param name="value">Value to hash</param>
        /// <param name="version">Version of the hash options</param>
        /// <param name="options">Delegate that returns hashing options based on the given version</param>
        /// <param name="salt">Salt that was used during hashing</param>
        /// <returns>Returns the hashed value.</returns>
        public static string Hash(string value, byte version, Func<byte, HashOptions> options, out string salt)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("The value cannot be null or empty.", nameof(value));

            var hashOptions = options(version);
            using var hashAlgorithm = hashOptions.Algorithm();

            var hashSize = hashAlgorithm.HashSize / 8;

            // Hashed value bytes are: {Version}{Salt}{Hash}
            // Version bytes are 8 bits (1 byte)
            var bytesSize = 1 + hashOptions.SaltSize + hashSize;
            Span<byte> hashedValue = new byte[bytesSize];

            var saltBytes = hashedValue.Slice(1, hashOptions.SaltSize);
            var hashBytes = hashedValue.Slice(1 + hashOptions.SaltSize, hashSize);
            Span<byte> bytes = new byte[hashOptions.SaltSize + Encoding.UTF8.GetByteCount(value)];

            // Fill the bytes
            hashedValue[0] = version;

            RandomNumberGenerator.Fill(saltBytes);
            saltBytes.CopyTo(bytes);
            
            Encoding.UTF8.GetBytes(value, bytes.Slice(hashOptions.SaltSize));

            hashAlgorithm.TryComputeHash(bytes, hashBytes, out _);

            salt = Convert.ToBase64String(saltBytes);
            return Convert.ToBase64String(hashedValue);
        }

        /// <summary>
        /// Hashes a given value.
        /// </summary>
        /// <param name="value">Value to hash</param>
        /// <param name="salt">Salt to use for hashing</param>
        /// <param name="version">Version of the hash options</param>
        /// <param name="options">Delegate that returns hashing options based on the given version</param>
        /// <returns>Returns the hashed value.</returns>
        public static string Hash(string value, string salt, byte version, Func<byte, HashOptions> options)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("The value cannot be null or empty.", nameof(value));

            var hashOptions = options(version);
            using var hashAlgorithm = hashOptions.Algorithm();

            var hashSize = hashAlgorithm.HashSize / 8;

            // Hashed value bytes are: {Version}{Salt}{Hash}
            // Version bytes are 8 bits (1 byte)
            var bytesSize = 1 + hashOptions.SaltSize + hashSize;
            Span<byte> hashedValue = new byte[bytesSize];

            var saltBytes = hashedValue.Slice(1, hashOptions.SaltSize);
            var hashBytes = hashedValue.Slice(1 + hashOptions.SaltSize, hashSize);
            Span<byte> bytes = new byte[hashOptions.SaltSize + Encoding.UTF8.GetByteCount(value)];

            // Fill the bytes
            hashedValue[0] = version;

            Convert.FromBase64String(salt).CopyTo(saltBytes);
            saltBytes.CopyTo(bytes);

            Encoding.UTF8.GetBytes(value, bytes.Slice(hashOptions.SaltSize));

            hashAlgorithm.TryComputeHash(bytes, hashBytes, out _);

            return Convert.ToBase64String(hashedValue);
        }

        /// <summary>
        /// Verifies if two values match.
        /// </summary>
        /// <param name="valueHashed">Hashed value to verify against</param>
        /// <param name="valuePlain">Plain value to check</param>
        /// <param name="options">Delegate that returns the correct hashing options based on the hashed value's version</param>
        /// <param name="version">Version of the hashed value</param>
        /// <returns>Returns the verification result.</returns>
        public static bool Verify(string valueHashed, string valuePlain, Func<byte, HashOptions> options, out byte version)
        {
            if (string.IsNullOrEmpty(valueHashed))
                throw new ArgumentException("The hashed value cannot be null or empty.", nameof(valueHashed));
            if (string.IsNullOrEmpty(valuePlain))
                throw new ArgumentException("The plain value cannot be null or empty.", nameof(valuePlain));

            // Find the version of the hashed value
            var bytes = Convert.FromBase64String(valueHashed).AsSpan();

            version = bytes[0];
            var hashOptions = options(version);
            using var hashAlgorithm = hashOptions.Algorithm();

            var hashSize = hashAlgorithm.HashSize / 8;

            // Hashed value bytes are: {Version}{Salt}{Hash}
            // Version bytes are 8 bits (1 byte)
            var expectedNumberOfBytes = 1 + hashOptions.SaltSize + hashSize;
            if (bytes.Length != expectedNumberOfBytes)
                throw new HashLengthMismatchException(expectedNumberOfBytes, bytes.Length);

            // Hash the plain value by using salt bytes of the hashed value
            // to compare the new hash bytes with the hashed values
            var saltBytes = bytes.Slice(1, hashOptions.SaltSize);
            var hashBytes = bytes.Slice(1 + hashOptions.SaltSize, hashSize);

            var valueBytesSize = Encoding.UTF8.GetByteCount(valuePlain);
            var combinedSize = hashOptions.SaltSize + valueBytesSize;

            Span<byte> combined = new byte[combinedSize];
            
            // Fill the bytes
            saltBytes.CopyTo(combined);
            Encoding.UTF8.GetBytes(valuePlain, combined.Slice(hashOptions.SaltSize));
            
            Span<byte> computedHash = new byte[hashSize];
            
            hashAlgorithm.TryComputeHash(combined, computedHash, out _);

            // To prevent timing attack, the comparison is made over CryptographicOperations
            return CryptographicOperations.FixedTimeEquals(hashBytes, computedHash);
        }
    }
}
