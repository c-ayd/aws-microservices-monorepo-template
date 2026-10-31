using AuthService.Application.Features.AccountEndpoints.CloseSession;
using Common.Test.Generators;

namespace AuthService.Test.Unit.Application.Features.AccountEndpoints.CloseSession
{
    public class CloseSessionValidatorTest
    {
        private readonly CloseSessionValidator _validator;

        public CloseSessionValidatorTest()
        {
            _validator = new CloseSessionValidator();
        }

        [Fact]
        public void Validate_WhenPasswordIsNull_ShouldReturnError()
        {
            // Arrange
            var request = new CloseSessionRequest(null);

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Single(errors);
        }

        [Fact]
        public void Validate_WhenPasswordIsValid_ShouldReturnNoError()
        {
            // Arrange
            var request = new CloseSessionRequest(PasswordGenerator.GenerateValid());

            // Act
            var errors = _validator.Validate(request);

            // Assert
            Assert.Empty(errors);
        }
    }
}
