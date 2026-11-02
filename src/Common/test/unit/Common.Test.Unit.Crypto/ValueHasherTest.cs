using System.Security.Cryptography;
using Common.Crypto;
using Common.Crypto.Exceptions;
using Common.Crypto.Structures;

namespace Common.Test.Unit.Crypto
{
    public class ValueHasherTest
    {
        [Fact]
        public void Hash_WhenIsCalled_ShouldHashValue()
        {
            // Arrange
            var value = new byte[] { 1, 2, 3, 4, 5 };
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);

            // Act
            var hashedValue = ValueHasher.Hash(value, version: 1, (version) => hashOptions);

            // Assert
            Assert.NotNull(hashedValue);
            Assert.NotEmpty(hashedValue);
            Assert.NotEqual(value, hashedValue);
        }

        [Fact]
        public void Hash_WhenSaltIsGiven_ShouldProduceSameHash()
        {
            // Arrange
            var value = new byte[] { 1, 2, 3, 4, 5 };
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);

            // Act
            var hashedValue = ValueHasher.Hash(value, version: 1, (version) => hashOptions, out var salt);
            var otherHashedValue = ValueHasher.Hash(value, salt, version: 1, (version) => hashOptions);

            // Assert
            Assert.Equal(hashedValue, otherHashedValue);
        }

        [Fact]
        public void Verify_WhenNumberOfBytesDoesNotMatchVersion_ShouldThrowException()
        {
            // Arrange
            var value = new byte[] { 1, 2, 3, 4, 5 };
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
            var value = new byte[] { 1, 2, 3, 4, 5 };
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);
            var hashedValue = ValueHasher.Hash(value, version: 1, (version) => hashOptions);

            // Act
            var result = ValueHasher.Verify(hashedValue, [1, 2, 3], (version) => hashOptions, out var version);

            // Assert
            Assert.False(result, "The verification returned true.");
        }

        [Fact]
        public void Verify_WhenValuesAreSame_ShouldReturnTrue()
        {
            // Arrange
            var value = new byte[] { 1, 2, 3, 4, 5 };
            var hashOptions = new HashOptions(Algorithm: SHA256.Create, SaltSize: 16);
            var hashedValue = ValueHasher.Hash(value, version: 1, (version) => hashOptions);

            // Act
            var result = ValueHasher.Verify(hashedValue, value, (version) => hashOptions, out var version);

            // Assert
            Assert.True(result, "The verification returned false.");
        }
    }
}
