using AuthService.Application.Features.AccountEndpoints.Login;
using Shared.Test.Generators;

namespace AuthService.Test.Unit.Application.Features.AccountEndpoints.Login
{
    public class LoginValidatorTest
    {
        private readonly LoginValidator _validator;

        public LoginValidatorTest()
        {
            _validator = new LoginValidator();
        }

        [Fact]
        public void Validate_WhenEmailAndPasswordAreNull_ShouldValidateContinuouslyAndReturnErrors()
        {
            // Arrange
            var request = new LoginRequest(null, null);

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void Validate_WhenEmailAndPasswordAreCorrect_ShouldReturnNoError()
        {
            // Arrange
            var request = new LoginRequest(EmailGenerator.Generate(), PasswordGenerator.GenerateValid());

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Empty(errors);
        }
    }
}
