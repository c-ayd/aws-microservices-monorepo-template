using System.Reflection;
using System.Security.Cryptography;
using AuthService.Application.Dtos.Crypto;
using AuthService.Infrastructure.Crypto;
using Shared.Test.Generators;

namespace AuthService.Test.Unit.Infrastructure.Crypto
{
    public class Pbkdf2Test
    {
        private readonly Pbkdf2 _pbkdf2;

        public Pbkdf2Test()
        {
            _pbkdf2 = new Pbkdf2();
        }

        [Fact]
        public void Hash_WhenPasswordIsValid_ShouldHashPassword()
        {
            // Act
            var password = PasswordGenerator.Generate();
            var passwordHashed = _pbkdf2.Hash(password);

            // Assert
            Assert.NotNull(passwordHashed);
            Assert.NotEmpty(passwordHashed);
            Assert.NotEqual(password, passwordHashed);
        }

        [Theory]
        [InlineData("")]
        [InlineData(null)]
        public void Hash_WhenPasswordIsInvalid_ShouldThrowException(string? password)
        {
            // Act
            var result = Record.Exception(() =>
            {
                _pbkdf2.Hash(password!);
            });

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ArgumentException>(result);
        }

        [Fact]
        public void Verify_WhenHashingVersionIsNotFound_ShouldReturnVersionNotFound()
        {
            // Arrange
            byte version = 0;
            var password = PasswordGenerator.Generate();
            var passwordHashed = _pbkdf2.Hash(password);

            byte[] hashBytes = Convert.FromBase64String(passwordHashed);
            hashBytes[0] = version;
            passwordHashed = Convert.ToBase64String(hashBytes);

            // Act
            var result = _pbkdf2.Verify(passwordHashed, password, out var passwordVersion);

            // Assert
            Assert.Equal(EPasswordVerificationResult.VersionNotFound, result);
            Assert.Equal(version, passwordVersion);
        }

        [Fact]
        public void Verify_WhenNumberOfBytesDoesNotMatchVersion_ShouldReturnLengthMismatch()
        {
            // Arrange
            byte version = byte.MaxValue;
            var password = PasswordGenerator.Generate();
            var passwordHashed = _pbkdf2.Hash(password);

            byte[] hashBytes = Convert.FromBase64String(passwordHashed);
            hashBytes[0] = version;
            passwordHashed = Convert.ToBase64String(hashBytes);

            var hashVersionType = typeof(Pbkdf2)
                .GetNestedType("HashVersion", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var hashVersionTypeCtor = hashVersionType.GetConstructor([typeof(HashAlgorithmName), typeof(int), typeof(int), typeof(int)])!;
            var hashVersionInstance = hashVersionTypeCtor.Invoke([HashAlgorithmName.SHA512, 100, 100, 100]);

            var hashVersionsDict = typeof(Pbkdf2)
                .GetField("_hashVersions", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(_pbkdf2)!;
            var hashVersionsDictType = hashVersionsDict.GetType();
            var addVersionMethod = hashVersionsDictType.GetMethod("Add", [typeof(byte), hashVersionType])!;
            addVersionMethod.Invoke(hashVersionsDict, [byte.MaxValue, hashVersionInstance]);

            // Act
            var result = _pbkdf2.Verify(passwordHashed, password, out var passwordVersion);

            // Assert
            Assert.Equal(EPasswordVerificationResult.LengthMismatch, result);
            Assert.Equal(version, passwordVersion);
        }

        [Fact]
        public void Verify_WhenPasswordIsDifferent_ShouldReturnFail()
        {
            // Arrange
            var password = PasswordGenerator.Generate();
            var passwordHashed = _pbkdf2.Hash(password);

            // Act
            var result = _pbkdf2.Verify(passwordHashed, password + "a", out _);

            // Assert
            Assert.Equal(EPasswordVerificationResult.Fail, result);
        }

        [Fact]
        public void Verify_WhenPasswordIsSameButVersionIsDifferent_ShouldReturnSuccessRehashNeeded()
        {
            // Arrange
            byte version = byte.MaxValue;
            var password = PasswordGenerator.Generate();
            var passwordHashed = _pbkdf2.Hash(password);

            byte[] hashBytes = Convert.FromBase64String(passwordHashed);
            hashBytes[0] = version;
            passwordHashed = Convert.ToBase64String(hashBytes);

            var currentVersion = typeof(Pbkdf2)
                .GetField("_currentVersion", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(_pbkdf2)!;
            var hashVersionsDict = typeof(Pbkdf2)
                .GetField("_hashVersions", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(_pbkdf2)!;
            var hashVersionType = typeof(Pbkdf2)
                .GetNestedType("HashVersion", BindingFlags.NonPublic)!;

            var hashVersionsDictType = hashVersionsDict.GetType();
            var currentHashVersion = hashVersionsDictType.GetProperty("Item")!
                .GetValue(hashVersionsDict, [currentVersion])!;

            var addVersionMethod = hashVersionsDictType.GetMethod("Add", [typeof(byte), hashVersionType])!;
            addVersionMethod.Invoke(hashVersionsDict, [byte.MaxValue, currentHashVersion]);

            // Act
            var result = _pbkdf2.Verify(passwordHashed, password, out var passwordVersion);

            // Assert
            Assert.Equal(EPasswordVerificationResult.SuccessRehashNeeded, result);
            Assert.Equal(version, passwordVersion);
        }

        [Fact]
        public void Verify_WhenPasswordAndVersionAreSame_ShouldReturnSuccess()
        {
            // Arrange
            var password = PasswordGenerator.Generate();
            var passwordHashed = _pbkdf2.Hash(password);

            // Act
            var result = _pbkdf2.Verify(passwordHashed, password, out _);

            // Assert
            Assert.Equal(EPasswordVerificationResult.Success, result);
        }

        [Theory]
        [InlineData("", "test")]
        [InlineData(null, "test")]
        [InlineData("test", "")]
        [InlineData("test", null)]
        public void Verify_WhenGivenValuesAreInvalid_ShouldThrowException(string? hashedPassword, string? password)
        {
            // Act
            var result = Record.Exception(() =>
            {
                _pbkdf2.Verify(hashedPassword!, password!, out _);
            });

            // Assert
            Assert.NotNull(result);
            Assert.IsType<ArgumentException>(result);
        }
    }
}
