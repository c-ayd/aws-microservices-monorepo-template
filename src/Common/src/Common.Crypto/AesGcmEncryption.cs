using System.Security.Cryptography;

namespace Common.Crypto
{
    /// <summary>
    /// Encrypts and decrypts values using AES-256-GCM.
    /// </summary>
    public static class AesGcmEncryption
    {
        private const int _nonceSize = 12;
        private const int _tagSize = 16;

        /// <summary>
        /// Encrypts a given value.
        /// </summary>
        /// <param name="value">Value to encrypt</param>
        /// <param name="key">Encryption key</param>
        /// <param name="deleteKey">Whether to set the key array to all zeros</param>
        /// <returns>Returns the encrypted value.</returns>
        public static byte[] Encrypt(ReadOnlySpan<byte> value, byte[] key, bool deleteKey = true)
        {
            // Encrypted value bytes are: {Nonce}{Tag}{Cipher}
            var encryptedDataLength = _nonceSize + _tagSize + value.Length;
            Span<byte> bytes = new byte[encryptedDataLength];

            var nonceBytes = bytes.Slice(0, _nonceSize);
            var tagBytes = bytes.Slice(_nonceSize, _tagSize);
            var cipherBytes = bytes.Slice(_nonceSize + _tagSize, value.Length);

            // Encrypt
            RandomNumberGenerator.Fill(nonceBytes);
            using var aesGcm = new AesGcm(key, _tagSize);
            aesGcm.Encrypt(nonceBytes, value, cipherBytes, tagBytes);

            if (deleteKey)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            return bytes.ToArray();
        }

        /// <summary>
        /// Decrypts a given value.
        /// </summary>
        /// <param name="encryptedValue">Encrypted value to decrypt</param>
        /// <param name="key">Encryption key that is used to encrypt the value</param>
        /// <param name="deleteKey">Whether to set the key array to all zeros</param>
        /// <returns>Returns the decrypted value.</returns>
        public static byte[] Decrypt(ReadOnlySpan<byte> encryptedValue, byte[] key, bool deleteKey = true)
        {
            // Encrypted value bytes are: {Nonce}{Tag}{Cipher}
            var cipherSize = encryptedValue.Length - (_nonceSize + _tagSize);

            var nonceBytes = encryptedValue.Slice(0, _nonceSize);
            var tagBytes = encryptedValue.Slice(_nonceSize, _tagSize);
            var cipherBytes = encryptedValue.Slice(_nonceSize + _tagSize, cipherSize);

            // Decrypt
            Span<byte> decryptedBytes = new byte[cipherSize];
            using var aesGcm = new AesGcm(key, _tagSize);
            aesGcm.Decrypt(nonceBytes, cipherBytes, tagBytes, decryptedBytes);

            if (deleteKey)
            {
                CryptographicOperations.ZeroMemory(key);
            }

            return decryptedBytes.ToArray();
        }

        /// <summary>
        /// Compares an encrypted value against a plain value.
        /// </summary>
        /// <param name="encryptedValue">Encrypted value to check</param>
        /// <param name="plainValue">Plain value to compare against</param>
        /// <param name="key">Encryption key that is used to encrypt the value</param>
        /// <param name="deleteKey">Whether to set the key array to all zeros</param>
        /// <returns>Returns the comparison result.</returns>
        public static bool Compare(ReadOnlySpan<byte> encryptedValue, ReadOnlySpan<byte> plainValue, byte[] key, bool deleteKey = true)
        {
            var decryptedValue = Decrypt(encryptedValue, key, deleteKey);

            // To prevent timing attack, the comparison is made over CryptographicOperations
            return CryptographicOperations.FixedTimeEquals(decryptedValue, plainValue);
        }
    }
}
