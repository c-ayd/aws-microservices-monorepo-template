using System.Security.Cryptography;
using Common.Crypto;
using Common.Crypto.Exceptions;
using Common.Crypto.Structures;
using Common.Test.Generators;

namespace Common.Test.Unit.Crypto
{
    public class ValueHasherTest
    {
        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Hash_WhenValueIsInvalid_ShouldThrowException(string? value)
        {
            // Arrange
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);

            // Act
            var exception = Record.Exception(() =>
            {
                ValueHasher.Hash(value!, version: 1, (version) => hashOptions);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void Hash_WhenParametersAreValid_ShouldHashValue()
        {
            // Arrange
            var value = StringGenerator.GeneratePrintableAscii();
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);

            // Act
            var hashedValue = ValueHasher.Hash(value, version: 1, (version) => hashOptions);

            // Assert
            Assert.NotNull(hashedValue);
            Assert.NotEmpty(hashedValue);
            Assert.NotEqual(value, hashedValue);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Verify_WhenHashedValueIsInvalid_ShouldThrowException(string? hashedValue)
        {
            // Arrange
            var value = StringGenerator.GeneratePrintableAscii();
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);

            // Act
            var exception = Record.Exception(() =>
            {
                ValueHasher.Verify(hashedValue!, value, (version) => hashOptions, out var version);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Verify_WhenPlainValueIsInvalid_ShouldThrowException(string? plaintext)
        {
            // Arrange
            var hashedValue = StringGenerator.GeneratePrintableAscii();
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);

            // Act
            var exception = Record.Exception(() =>
            {
                ValueHasher.Verify(hashedValue, plaintext!, (version) => hashOptions, out var version);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<ArgumentException>(exception);
        }

        [Fact]
        public void Verify_WhenNumberOfBytesDoesNotMatchVersion_ShouldThrowException()
        {
            // Arrange
            var value = StringGenerator.GeneratePrintableAscii();
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);
            var hashedValue = ValueHasher.Hash(value, version: 1, (version) => hashOptions);

            // Act
            var exception = Record.Exception(() =>
            {
                ValueHasher.Verify(hashedValue, value, (version) => new HashOptions(SHA256.Create, 32), out var version);
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<HashLengthMismatchException>(exception);
        }

        [Fact]
        public void Verify_WhenValuesAreDifferent_ShouldReturnFalse()
        {
            // Arrange
            var value = StringGenerator.GeneratePrintableAscii();
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);
            var hashedValue = ValueHasher.Hash(value, version: 1, (version) => hashOptions);

            // Act
            var result = ValueHasher.Verify(hashedValue, value + "a", (version) => hashOptions, out var version);

            // Assert
            Assert.False(result, "The verification returned true.");
        }

        [Fact]
        public void Verify_WhenValuesAreSame_ShouldReturnTrue()
        {
            // Arrange
            var value = StringGenerator.GeneratePrintableAscii();
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);
            var hashedValue = ValueHasher.Hash(value, version: 1, (version) => hashOptions);

            // Act
            var result = ValueHasher.Verify(hashedValue, value, (version) => hashOptions, out var version);

            // Assert
            Assert.True(result, "The verification returned false.");
        }
    }
}
