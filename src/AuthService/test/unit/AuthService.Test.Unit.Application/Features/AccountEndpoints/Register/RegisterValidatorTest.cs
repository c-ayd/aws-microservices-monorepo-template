using AuthService.Application.Features.AccountEndpoints.Register;
using Common.Test.Generators;

namespace AuthService.Test.Unit.Application.Features.AccountEndpoints.Register
{
    public class RegisterValidatorTest
    {
        private readonly RegisterValidator _validator;

        public RegisterValidatorTest()
        {
            _validator = new RegisterValidator();
        }

        [Fact]
        public void Validate_WhenEmailAndPasswordAreNull_ShouldValidateContinuouslyAndReturnErrors()
        {
            // Arrange
            var request = new RegisterRequest(null, null);

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Equal(2, errors.Count);
        }

        [Fact]
        public void Validate_WhenEmailAndPasswordAreCorrect_ShouldReturnNoError()
        {
            // Arrange
            var request = new RegisterRequest(EmailGenerator.Generate(), PasswordGenerator.GenerateValid());

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Empty(errors);
        }
    }
}
