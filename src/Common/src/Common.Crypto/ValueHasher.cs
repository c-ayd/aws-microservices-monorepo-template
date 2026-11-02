using System.Security.Cryptography;
using Common.Crypto.Exceptions;
using Common.Crypto.Structures;

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
        public static byte[] Hash(ReadOnlySpan<byte> value, byte version, Func<byte, HashOptions?> options)
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
        public static byte[] Hash(ReadOnlySpan<byte> value, byte version, Func<byte, HashOptions?> options, out byte[] salt)
        {
            var hashOptions = options(version);
            if (hashOptions == null)
                throw new HashVersionNotFoundException(version);

            using var hashAlgorithm = hashOptions.Algorithm();

            var hashSize = hashAlgorithm.HashSize / 8;

            // Hashed value bytes are: {Version}{Salt}{Hash}
            // Version bytes are 8 bits (1 byte)
            Span<byte> hashedValue = new byte[1 + hashOptions.SaltSize + hashSize];
            hashedValue[0] = version;
            var saltBytes = hashedValue.Slice(1, hashOptions.SaltSize);
            var hashBytes = hashedValue.Slice(1 + hashOptions.SaltSize, hashSize);

            // Fill the bytes
            Span<byte> bytes = new byte[hashOptions.SaltSize + value.Length];
            
            RandomNumberGenerator.Fill(saltBytes);
            saltBytes.CopyTo(bytes);
            value.CopyTo(bytes.Slice(hashOptions.SaltSize));

            hashAlgorithm.TryComputeHash(bytes, hashBytes, out _);

            salt = saltBytes.ToArray();
            return hashedValue.ToArray();
        }

        /// <summary>
        /// Hashes a given value.
        /// </summary>
        /// <param name="value">Value to hash</param>
        /// <param name="salt">Salt to use for hashing</param>
        /// <param name="version">Version of the hash options</param>
        /// <param name="options">Delegate that returns hashing options based on the given version</param>
        /// <returns>Returns the hashed value.</returns>
        public static byte[] Hash(ReadOnlySpan<byte> value, ReadOnlySpan<byte> salt, byte version, Func<byte, HashOptions?> options)
        {
            var hashOptions = options(version);
            if (hashOptions == null)
                throw new HashVersionNotFoundException(version);
            
            using var hashAlgorithm = hashOptions.Algorithm();

            var hashSize = hashAlgorithm.HashSize / 8;

            // Hashed value bytes are: {Version}{Salt}{Hash}
            // Version bytes are 8 bits (1 byte)
            Span<byte> hashedValue = new byte[1 + hashOptions.SaltSize + hashSize];
            hashedValue[0] = version;
            var saltBytes = hashedValue.Slice(1, hashOptions.SaltSize);
            var hashBytes = hashedValue.Slice(1 + hashOptions.SaltSize, hashSize);

            salt.CopyTo(saltBytes);

            // Fill the bytes
            Span<byte> bytes = new byte[hashOptions.SaltSize + value.Length];

            salt.CopyTo(bytes);
            value.CopyTo(bytes.Slice(hashOptions.SaltSize));

            hashAlgorithm.TryComputeHash(bytes, hashBytes, out _);

            return hashedValue.ToArray();
        }

        /// <summary>
        /// Verifies if two values match.
        /// </summary>
        /// <param name="hashedValue">Hashed value to verify against</param>
        /// <param name="plaintextValue">Plain value to check</param>
        /// <param name="options">Delegate that returns the correct hashing options based on the hashed value's version</param>
        /// <param name="version">Version of the hashed value</param>
        /// <returns>Returns the verification result.</returns>
        public static bool Verify(ReadOnlySpan<byte> hashedValue, ReadOnlySpan<byte> plaintextValue, Func<byte, HashOptions?> options, out byte version)
        {
            version = hashedValue[0];
            var hashOptions = options(version);
            if (hashOptions == null)
                throw new HashVersionNotFoundException(version);
            
            using var hashAlgorithm = hashOptions.Algorithm();

            var hashSize = hashAlgorithm.HashSize / 8;

            // Hashed value bytes are: {Version}{Salt}{Hash}
            // Version bytes are 8 bits (1 byte)
            var expectedNumberOfBytes = 1 + hashOptions.SaltSize + hashSize;
            if (hashedValue.Length != expectedNumberOfBytes)
                throw new HashLengthMismatchException(expectedNumberOfBytes, hashedValue.Length);

            // Hash the plain value by using salt bytes of the hashed value
            // to compare the new hash bytes with the hashed values
            var saltBytes = hashedValue.Slice(1, hashOptions.SaltSize);
            var hashBytes = hashedValue.Slice(1 + hashOptions.SaltSize, hashSize);

            Span<byte> compareBytes = new byte[hashOptions.SaltSize + plaintextValue.Length];
            
            // Fill the bytes
            saltBytes.CopyTo(compareBytes);

            plaintextValue.CopyTo(compareBytes.Slice(hashOptions.SaltSize));
            
            Span<byte> computedHash = new byte[hashSize];
            hashAlgorithm.TryComputeHash(compareBytes, computedHash, out _);

            // To prevent timing attack, the comparison is made over CryptographicOperations
            return CryptographicOperations.FixedTimeEquals(hashBytes, computedHash);
        }
    }
}
