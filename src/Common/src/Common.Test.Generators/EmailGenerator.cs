using System.Text;

namespace Common.Test.Generators
{
    /// <summary>
    /// Generates random email addresses for test cases.
    /// </summary>
    public static class EmailGenerator
    {
        /// <summary>
        /// Generates a random email address.
        /// </summary>
        /// <param name="usernameLength">Length of the username part of the generated email</param>
        /// <param name="domainLength">Length of the domain part of the generated email</param>
        /// <param name="tldLength">Length of the top-level domain part of the generated email</param>
        /// <returns>Returns the generated email address.</returns>
        public static string Generate(int usernameLength = 10, int domainLength = 10, int tldLength = 3)
        {
            if (usernameLength <= 0)
                throw new ArgumentException("The length of the username part must be a postive number", nameof(usernameLength));
            if (domainLength <= 0)
                throw new ArgumentException("The length of the domain part must be a postive number", nameof(domainLength));
            if (tldLength <= 0)
                throw new ArgumentException("The length of the top-level domain part must be a postive number", nameof(tldLength));

            var builder = new StringBuilder();

            builder.Append(AsciiHelper.GetRandomLowercaseChar());
            usernameLength -= 2;
            if (usernameLength > 0)
            {
                for (int i = 0; i < usernameLength; ++i)
                {
                    if (Random.Shared.Next(0, 2) == 0)
                    {
                        builder.Append(AsciiHelper.GetRandomLowercaseChar());
                    }
                    else
                    {
                        builder.Append(AsciiHelper.GetRandomDigitChar());
                    }
                }
            }
            builder.Append(AsciiHelper.GetRandomLowercaseChar());

            builder.Append('@');

            builder.Append(AsciiHelper.GetRandomLowercaseChar());
            domainLength -= 2;
            if (domainLength > 0)
            {
                for (int i = 0; i < domainLength; ++i)
                {
                    if (Random.Shared.Next(0, 2) == 0)
                    {
                        builder.Append(AsciiHelper.GetRandomLowercaseChar());
                    }
                    else
                    {
                        builder.Append(AsciiHelper.GetRandomDigitChar());
                    }
                }
            }
            builder.Append(AsciiHelper.GetRandomLowercaseChar());

            builder.Append('.');

            for (int i = 0; i < tldLength; ++i)
            {
                builder.Append(AsciiHelper.GetRandomLowercaseChar());
            }

            return builder.ToString();
        }
    }
}
