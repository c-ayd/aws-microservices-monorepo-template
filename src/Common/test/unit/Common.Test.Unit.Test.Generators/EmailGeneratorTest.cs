using Common.Test.Generators;

namespace Common.Test.Unit.Test.Generators
{
    public class EmailGeneratorTest
    {
        [Theory]
        [InlineData(0, 10, 3)]
        [InlineData(-1, 10, 3)]
        [InlineData(10, 0, 3)]
        [InlineData(10, -1, 3)]
        [InlineData(10, 10, 0)]
        [InlineData(10, 10, -1)]
        public void Generate_WhenParametersAreInvalid_ShouldThrowException(int usernameLength, int domainLength, int tldLength)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                EmailGenerator.Generate(usernameLength, domainLength, tldLength);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void Generate_WhenParametersAreValid_ShouldGenerateEmail()
        {
            // Act
            var usernameLength = 10;
            var domainLength = 10;
            var tldLength = 10;
            var generatedEmail = EmailGenerator.Generate(usernameLength, domainLength, tldLength);

            // Assert
            Assert.NotNull(generatedEmail);
            Assert.NotEmpty(generatedEmail);

            if (!generatedEmail.Contains('@') || !generatedEmail.Contains('.'))
                Assert.Fail($"The generated email does not contain @ and . symbols. Email: {generatedEmail}");

            var atSymbolIndex = generatedEmail.IndexOf('@');
            var dotSymbolIndex = generatedEmail.IndexOf('.');

            var usernamePart = generatedEmail.Substring(0, atSymbolIndex);
            Assert.Equal(usernameLength, usernamePart.Length);
            if (usernamePart[0] < 97 || usernamePart[0] > 122)
                Assert.Fail($"The username part starts does not start with an alphabetic character. Username: {usernamePart}");
            if (usernamePart[usernamePart.Length - 1] < 97 || usernamePart[usernamePart.Length - 1] > 122)
                Assert.Fail($"The username part starts does not end with an alphabetic character. Username: {usernamePart}");

            var domainPart = generatedEmail.Substring(atSymbolIndex + 1, dotSymbolIndex - atSymbolIndex - 1);
            Assert.Equal(domainLength, domainPart.Length);
            if (domainPart[0] < 97 || domainPart[0] > 122)
                Assert.Fail($"The domain part starts does not start with an alphabetic character. Domain: {domainPart}");
            if (domainPart[domainPart.Length - 1] < 97 || domainPart[domainPart.Length - 1] > 122)
                Assert.Fail($"The domain part starts does not end with an alphabetic character. Domain: {domainPart}");

            var tldPart = generatedEmail.Substring(dotSymbolIndex + 1);
            Assert.Equal(tldLength, tldPart.Length);
            if (tldPart[0] < 97 || tldPart[0] > 122)
                Assert.Fail($"The TLD part starts does not start with an alphabetic character. TLD: {tldPart}");
            if (tldPart[tldPart.Length - 1] < 97 || tldPart[tldPart.Length - 1] > 122)
                Assert.Fail($"The TLD part starts does not end with an alphabetic character. TLD: {tldPart}");
        }
    }
}
