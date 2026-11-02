using AuthService.Application.Features.AccountEndpoints.ResetPassword;
using Common.Test.Generators;

namespace AuthService.Test.Unit.Application.Features.AccountEndpoints.ResetPassword
{
    public class ResetPasswordValidatorTest
    {
        private readonly ResetPasswordValidator _validator;

        public ResetPasswordValidatorTest()
        {
            _validator = new ResetPasswordValidator();
        }

        [Fact]
        public void Validate_WhenPasswordIsNull_ShouldReturnError()
        {
            // Arranage
            var request = new ResetPasswordRequest(null, null);

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Single(errors);
        }

        [Fact]
        public void Validate_WhenPasswordIsValid_ShouldReturnNoError()
        {
            // Arranage
            var request = new ResetPasswordRequest(PasswordGenerator.GenerateValid(), null);

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Empty(errors);
        }
    }
}
