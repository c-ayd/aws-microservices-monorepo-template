using AuthService.Application.Validations.Constraints;
using AuthService.Application.Validations.Shared;
using Common.Http.Response.Structures;
using Common.Test.Generators;

namespace AuthService.Test.Unit.Application.Validations.Shared
{
    public class PasswordValidatorTest
    {
        private readonly PasswordValidator _validator = new PasswordValidator();

        [Fact]
        public void Validate_WhenPasswordLengthIsWrong_ShouldReturnError()
        {
            // Arrange
            var passwords = new List<string>()
            {
                PasswordGenerator.Generate(
                    includeUppercase: true,
                    includeLowercase: true,
                    includeDigit: true,
                    includeSpecialChars: true,
                    specialChars: AccountConstraints.PasswordSpecialCharacters,
                    length: AccountConstraints.PasswordMinLength - 1),
                PasswordGenerator.Generate(
                    includeUppercase: true,
                    includeLowercase: true,
                    includeDigit: true,
                    includeSpecialChars: true,
                    specialChars: AccountConstraints.PasswordSpecialCharacters,
                    length: AccountConstraints.EmailMaxLength + 1)
            };

            // Act
            var errors = new List<ErrorItem>();
            foreach (var password in passwords)
            {
                errors.AddRange(_validator.Validate(password));
            }

            // Assert
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void Validate_WhenFormatIsWrong_ShouldReturnError()
        {
            // Arrange
            var passwords = new List<string>()
            {
                PasswordGenerator.Generate(
                    includeUppercase: false,
                    includeLowercase: true,
                    includeDigit: true,
                    includeSpecialChars: true,
                    specialChars: AccountConstraints.PasswordSpecialCharacters,
                    length: AccountConstraints.PasswordMinLength),
                PasswordGenerator.Generate(
                    includeUppercase: true,
                    includeLowercase: false,
                    includeDigit: true,
                    includeSpecialChars: true,
                    specialChars: AccountConstraints.PasswordSpecialCharacters,
                    length: AccountConstraints.PasswordMinLength),
                PasswordGenerator.Generate(
                    includeUppercase: true,
                    includeLowercase: true,
                    includeDigit: false,
                    includeSpecialChars: true,
                    specialChars: AccountConstraints.PasswordSpecialCharacters,
                    length: AccountConstraints.PasswordMinLength),
                PasswordGenerator.Generate(
                    includeUppercase: true,
                    includeLowercase: true,
                    includeDigit: true,
                    includeSpecialChars: false,
                    length: AccountConstraints.PasswordMinLength)
            };

            // Act
            var errors = new List<ErrorItem>();
            foreach (var password in passwords)
            {
                errors.AddRange(_validator.Validate(password));
            }

            // Assert
            Assert.Equal(4, errors.Count);
        }

        [Fact]
        public void Validate_WhenPasswordIsInCorrectFormat_ShouldReturnNoError()
        {
            // Arrange
            var password = PasswordGenerator.Generate(
                    includeUppercase: true,
                    includeLowercase: true,
                    includeDigit: true,
                    includeSpecialChars: true,
                    specialChars: AccountConstraints.PasswordSpecialCharacters,
                    length: AccountConstraints.PasswordMinLength);

            // Act
            var errors = _validator.Validate(password);

            // Assert
            Assert.Empty(errors);
        }
    }
}
