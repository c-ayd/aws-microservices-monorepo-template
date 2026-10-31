using System.Text;
using Common.Test.Generators.Extensions;

namespace Common.Test.Generators
{
    /// <summary>
    /// Generates random passwords for test cases.
    /// </summary>
    public static class PasswordGenerator
    {
        /// <summary>
        /// Generates a random password.
        /// </summary>
        /// <param name="includeUppercase">Whether to include at least one uppercase letter in the generated password</param>
        /// <param name="includeLowercase">Whether to include at least one lowercase letter in the generated password</param>
        /// <param name="includeDigit">Whether to include at least one digit in the generated password</param>
        /// <param name="includeSpecialChars">Whether to include at least one special character in the generated password</param>
        /// <param name="specialChars">Allowed special characters for the generated password</param>
        /// <param name="length">Length of the generated password</param>
        /// <returns>Returns the generated password.</returns>
        public static string Generate(
            bool includeUppercase = true,
            bool includeLowercase = true,
            bool includeDigit = true,
            bool includeSpecialChars = false,
            string? specialChars = null,
            int length = 10)
        {
            if (includeSpecialChars && string.IsNullOrEmpty(specialChars))
                throw new ArgumentException("The special characters must be defined if the password must include special characters.", nameof(specialChars));
            if (length <= 0)
                throw new ArgumentException("The length must be a positive number.", nameof(length));

            var builder = new StringBuilder();

            // To enforce the password rules
            int minLength = 0;
            var categories = new List<int>();
            if (includeUppercase)
            {
                builder.Append(AsciiHelper.GetRandomUppercaseChar());
                ++minLength;
                categories.Add(0);      // Uppercase is marked as 0
            }
            if (includeLowercase)
            {
                builder.Append(AsciiHelper.GetRandomLowercaseChar());
                ++minLength;
                categories.Add(1);      // Lowercase is marked as 1
            }
            if (includeDigit)
            {
                builder.Append(AsciiHelper.GetRandomDigitChar());
                ++minLength;
                categories.Add(2);      // Digit is marked as 2
            }
            if (includeSpecialChars)
            {
                builder.Append(specialChars![Random.Shared.Next(0, specialChars.Length)]);
                ++minLength;
                categories.Add(3);      // Special character is marked as 3
            }

            if (length <= minLength)
                return builder.Shuffle().ToString();

            for (int i = minLength; i < length; ++i)
            {
                var category = categories[Random.Shared.Next(0, categories.Count)];
                if (category == 0)
                {
                    builder.Append(AsciiHelper.GetRandomUppercaseChar());
                }
                else if (category == 1)
                {
                    builder.Append(AsciiHelper.GetRandomLowercaseChar());
                }
                else if (category == 2)
                {
                    builder.Append(AsciiHelper.GetRandomDigitChar());
                }
                else
                {
                    builder.Append(specialChars![Random.Shared.Next(0, specialChars.Length)]);
                }
            }

            return builder.Shuffle().ToString();
        }

        public static string GenerateValid()
        {
            return Generate(
                includeUppercase: true,
                includeLowercase: true,
                includeDigit: true,
                includeSpecialChars: true,
                specialChars: "+-.",
                length: 20);
        }
    }
}
