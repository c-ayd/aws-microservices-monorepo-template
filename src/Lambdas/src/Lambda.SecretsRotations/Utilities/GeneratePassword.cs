using System.Security.Cryptography;
using System.Text;

namespace Lambda.SecretsRotations.Utilities
{
    public static partial class Utility
    {
        private const string GeneratePassword_LowercaseChars = "abcdefghijkmnopqrstuvwxyz";
        private const string GeneratePassword_UppercaseChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        private const string GeneratePassword_Digits = "0123456789";
        private const string GeneratePassword_AllChars = 
            GeneratePassword_LowercaseChars +
            GeneratePassword_UppercaseChars +
            GeneratePassword_Digits;

        /// <summary>
        /// Generates a random password including at least one lowercase, one uppercase and one digit.
        /// </summary>
        /// <param name="length">Length of the password</param>
        /// <returns>Returns the generated password.</returns>
        public static string GeneratePassword(int length)
        {
            var password = new StringBuilder(length);

            // Fill password
            byte[] randomBytes = RandomNumberGenerator.GetBytes(length);
            for (int i = 0; i < length; i++)
            {
                password.Append(GeneratePassword_AllChars[randomBytes[i] % GeneratePassword_AllChars.Length]);
            }

            // At least one char from defined chars in the password
            password[0] = GeneratePassword_LowercaseChars[randomBytes[0] % GeneratePassword_LowercaseChars.Length];
            password[1] = GeneratePassword_UppercaseChars[randomBytes[0] % GeneratePassword_UppercaseChars.Length];
            password[2] = GeneratePassword_Digits[randomBytes[0] % GeneratePassword_Digits.Length];

            // Shuffle
            Span<byte> shuffleBytes = new byte[length];
            RandomNumberGenerator.Fill(shuffleBytes);

            for (int i = length - 1; i > 0; --i)
            {
                int j = shuffleBytes[i] % (i + 1);
                (password[i], password[j]) = (password[j], password[i]);
            }

            return password.ToString();
        }
    }
}
