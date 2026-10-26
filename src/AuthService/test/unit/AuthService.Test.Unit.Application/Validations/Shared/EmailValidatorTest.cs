using AuthService.Application.Validations.Constraints;
using AuthService.Application.Validations.Shared;
using Shared.Test.Generators;

namespace AuthService.Test.Unit.Application.Validations.Shared
{
    public class EmailValidatorTest
    {
        private readonly EmailValidator _validator = new EmailValidator();

        [Theory]
        [InlineData("")]
        [InlineData("abc")]
        [InlineData("abc@")]
        public void Validate_WhenEmailIsInvalid_ShouldReturnError(string email)
        {
            // Act
            var errors = _validator.Validate(email);

            // Assert
            Assert.Single(errors);
        }

        [Fact]
        public void Validate_WhenEmailIsLong_ShouldReturnError()
        {
            // Arrange
            var email = EmailGenerator.Generate(
                usernameLength: AccountConstraints.EmailMaxLength + 1,
                domainLength: 3,
                tldLength: 3);

            // Act
            var errors = _validator.Validate(email);

            // Assert
            Assert.Single(errors);
        }

        [Fact]
        public void Validate_WhenEmailIsValid_ShouldReturnNoError()
        {
            // Arrange
            var email = EmailGenerator.Generate();

            // Act
            var errors = _validator.Validate(email);

            // Assert
            Assert.Empty(errors);
        }
    }
}
