using AuthService.Application.Errors;
using AuthService.Application.Validations.Shared;
using Common.Http.Response.Structures;
using Common.Http.Validation;

namespace AuthService.Application.Features.AccountEndpoints.Login
{
    public class LoginValidator : IValidator<LoginRequest>
    {
        public List<ErrorItem> Validate(LoginRequest value)
        {
            var errors = new List<ErrorItem>();

            if (value.Email == null)
            {
                errors.Add(ServiceErrors.EmailRequiredError);
            }
            else
            {
                errors.AddRange(new EmailValidator().Validate(value.Email));
            }
            
            if (value.Password == null)
            {
                errors.Add(ServiceErrors.PasswordRequiredError);
            }

            return errors;
        }
    }
}
