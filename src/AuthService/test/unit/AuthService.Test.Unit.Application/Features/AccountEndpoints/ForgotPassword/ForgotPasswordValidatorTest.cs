using AuthService.Application.Features.AccountEndpoints.ForgotPassword;
using Common.Test.Generators;

namespace AuthService.Test.Unit.Application.Features.AccountEndpoints.ForgotPassword
{
    public class ForgotPasswordValidatorTest
    {
        private readonly ForgotPasswordValidator _validator;

        public ForgotPasswordValidatorTest()
        {
            _validator = new ForgotPasswordValidator();
        }

        [Fact]
        public void Validate_WhenEmailIsNull_ShouldReturnError()
        {
            // Arrange
            var request = new ForgotPasswordRequest(null);

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Single(errors);
        }

        [Fact]
        public void Validate_WhenEmailIsCorrect_ShouldReturnNoError()
        {
            // Arrange
            var request = new ForgotPasswordRequest(EmailGenerator.Generate());

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Empty(errors);
        }
    }
}
