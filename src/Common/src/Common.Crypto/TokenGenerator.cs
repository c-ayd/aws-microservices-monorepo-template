using System.Security.Cryptography;
using Microsoft.AspNetCore.WebUtilities;
using Common.Crypto.Exceptions;

namespace Common.Crypto
{
    /// <summary>
    /// Generates random tokens.
    /// </summary>
    public static class TokenGenerator
    {
        /// <summary>
        /// Generates a random token using Base64 encoding, which is not URL safe.
        /// For the URL safe version, use <see cref="TokenGenerator.GenerateBase64UrlSafe(int)"/>. 
        /// </summary>
        /// <param name="numberOfBytes">The number of random bytes for the generation</param>
        /// <returns>Returns the generated token.</returns>
        public static string Generate(int numberOfBytes = 32)
        {
            GenerateRandomData(numberOfBytes, out var bytes);
            return Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// Generates a random URL safe token using Base64 encoding.
        /// </summary>
        /// <param name="numberOfBytes">The number of random bytes for the generation</param>
        /// <returns>Returns the generated URL safe token.</returns>
        public static string GenerateBase64UrlSafe(int numberOfBytes = 32)
        {
            GenerateRandomData(numberOfBytes, out var bytes);
            return WebEncoders.Base64UrlEncode(bytes);
        }

        private static void GenerateRandomData(int numberOfBytes, out byte[] bytes)
        {
            if (numberOfBytes <= 0)
                throw new InvalidNumberOfBytesException(numberOfBytes);

            bytes = new byte[numberOfBytes];
            RandomNumberGenerator.Fill(bytes);
        }
    }
}
