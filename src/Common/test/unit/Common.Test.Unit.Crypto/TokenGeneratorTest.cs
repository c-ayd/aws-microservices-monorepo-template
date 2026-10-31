using Common.Crypto;
using Common.Crypto.Exceptions;

namespace Common.Test.Unit.Crypto
{
    public class TokenGeneratorTest
    {
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void Generate_WhenInvalidNumberOfBytesIsGiven_ShouldThrowException(int numberOfBytes)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                TokenGenerator.Generate(numberOfBytes);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<InvalidNumberOfBytesException>(exception);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(48)]
        [InlineData(64)]
        public void Generate_WhenIsCalled_ShouldGenerateTokenWithCorrectLength(int numberOfBytes)
        {
            // Arrange
            var tokenLength = (int)(Math.Ceiling(numberOfBytes / 3.0) * 4);

            // Act
            var token = TokenGenerator.Generate(numberOfBytes);

            // Assert
            Assert.Equal(tokenLength, token.Length);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        public void GenerateBase64UrlSafe_WhenInvalidNumberOfBytesIsGiven_ShouldThrowException(int numberOfBytes)
        {
            // Act
            var exception = Record.Exception(() =>
            {
                TokenGenerator.GenerateBase64UrlSafe(numberOfBytes);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<InvalidNumberOfBytesException>(exception);
        }

        [Theory]
        [InlineData(16)]
        [InlineData(32)]
        [InlineData(48)]
        [InlineData(64)]
        public void GenerateBase64UrlSafe_WhenIsCalled_ShouldGenerateTokenWithCorrectLength(int numberOfBytes)
        {
            // Arrange
            var tokenLength = (int)(Math.Ceiling(numberOfBytes / 3.0) * 4);
            var paddingChars = (numberOfBytes % 3) switch
            {
                1 => 2,
                2 => 1,
                _ => 0
            };
            var urlSafeLength = tokenLength - paddingChars;

            // Act
            var token = TokenGenerator.GenerateBase64UrlSafe(numberOfBytes);

            // Assert
            Assert.Equal(urlSafeLength, token.Length);
        }
    }
}
