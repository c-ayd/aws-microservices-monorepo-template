using System.Text;
using Common.Test.Generators.Extensions;

namespace Common.Test.Generators
{
    /// <summary>
    /// Generates random strings for test cases.
    /// </summary>
    public static class StringGenerator
    {
        /// <summary>
        /// Generates a random string using printable ASCII characters.
        /// </summary>
        /// <param name="length">Length of the generated string</param>
        /// <returns>Returns the generated string.</returns>
        public static string GeneratePrintableAscii(int length = 10)
        {
            if (length <= 0)
                throw new ArgumentException("The length must be a positive number", nameof(length));

            var builder = new StringBuilder();

            for (int i = 0; i < length; ++i)
            {
                builder.Append(AsciiHelper.GetRandomPrintableChar());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Generates a random string using alphabetic characters.
        /// If a given length is lower than 2, the length is enforced to become 2 to include at least
        /// one uppercase and one lowercase.
        /// </summary>
        /// <param name="length">Length of the generated string</param>
        /// <returns>Returns the generated string.</returns>
        public static string GenerateAlpha(int length = 10)
        {
            if (length <= 0)
                throw new ArgumentException("The length must be a positive number", nameof(length));

            var builder = new StringBuilder();

            // To enforce at least one lowercase and one uppercase
            builder.Append(AsciiHelper.GetRandomUppercaseChar());
            builder.Append(AsciiHelper.GetRandomLowercaseChar());

            if (length <= 2)
                return builder.Shuffle().ToString();

            for (int i = 2; i < length; ++i)
            {
                if (Random.Shared.Next(0, 2) == 0)
                {
                    builder.Append(AsciiHelper.GetRandomUppercaseChar());
                }
                else
                {
                    builder.Append(AsciiHelper.GetRandomLowercaseChar());
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Generates a random string using alphabetic and numeric characters.
        /// If a given length is lower than 3, the length is enforced to become 3 to include at least
        /// one uppercase, one lowercase and one digit.
        /// </summary>
        /// <param name="length">Length of the generated string</param>
        /// <returns>Returns the generated string.</returns>
        public static string GenerateAlphanumeric(int length = 10)
        {
            if (length <= 0)
                throw new ArgumentException("The length must be a positive number", nameof(length));

            var builder = new StringBuilder();

            // To enforce at least one lowercase, one uppercase and one digit
            builder.Append(AsciiHelper.GetRandomUppercaseChar());
            builder.Append(AsciiHelper.GetRandomLowercaseChar());
            builder.Append(AsciiHelper.GetRandomLowercaseChar());

            if (length <= 3)
                return builder.Shuffle().ToString();

            for (int i = 3; i < length; ++i)
            {
                var category = Random.Shared.Next(0, 3);
                if (category == 0)
                {
                    builder.Append(AsciiHelper.GetRandomUppercaseChar());
                }
                else if (category == 1)
                {
                    builder.Append(AsciiHelper.GetRandomLowercaseChar());
                }
                else
                {
                    builder.Append(AsciiHelper.GetRandomDigitChar());
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Generates a random string using numeric characters.
        /// </summary>
        /// <param name="length">Length of the generated string</param>
        /// <returns>Returns the generated string.</returns>
        public static string GenerateNumeric(int length = 10)
        {
            if (length <= 0)
                throw new ArgumentException("The length must be a positive number", nameof(length));

            var builder = new StringBuilder();

            for (int i = 0; i < length; ++i)
            {
                builder.Append(AsciiHelper.GetRandomDigitChar());
            }

            return builder.ToString();
        }

        /// <summary>
        /// Generates a random string using the given character list.
        /// </summary>
        /// <param name="charList">List of characters to generate string randomly</param>
        /// <param name="length">Length of the generated string</param>
        /// <returns>Returns the generated string.</returns>
        public static string GenerateCustom(string charList, int length = 10)
        {
            if (string.IsNullOrEmpty(charList))
                throw new ArgumentException("The character list cannot be null or empty", nameof(charList));
            if (length <= 0)
                throw new ArgumentException("The length must be a positive number", nameof(length));

            var builder = new StringBuilder();

            for (int i = 0; i < length; ++i)
            {
                builder.Append(charList[Random.Shared.Next(0, charList.Length)]);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Appends a random string to a given string value.
        /// </summary>
        /// <param name="value">Initial value of the string</param>
        /// <param name="charList">List of characters to generate the random string that is appended</param>
        /// <param name="length">Length of the generated string</param>
        /// <returns>Returns the generated string.</returns>
        public static string AppendCustom(string value, string charList, int length = 10)
        {
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException("The value cannot be null or empty", nameof(charList));
            if (string.IsNullOrEmpty(charList))
                throw new ArgumentException("The character list cannot be null or empty", nameof(charList));
            if (length <= 0)
                throw new ArgumentException("The length must be a positive number", nameof(length));

            var builder = new StringBuilder(value);

            for (int i = 0; i < length; ++i)
            {
                builder.Append(charList[Random.Shared.Next(0, charList.Length)]);
            }

            return builder.ToString();
        }
    }
}
