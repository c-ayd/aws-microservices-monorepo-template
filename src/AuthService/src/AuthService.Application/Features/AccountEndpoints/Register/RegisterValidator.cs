using AuthService.Application.Validations.Shared;
using Common.Http.Response.Structures;
using Common.Http.Validation;

namespace AuthService.Application.Features.AccountEndpoints.Register
{
    public class RegisterValidator : IValidator<RegisterRequest>
    {
        public List<ErrorItem> Validate(RegisterRequest value)
        {
            var errors = new List<ErrorItem>();

            if (value.Email == null)
            {
                errors.Add(new ErrorItem("auth_email_required", "The email address is required."));
            }
            else
            {
                errors.AddRange(new EmailValidator().Validate(value.Email));
            }

            if (value.Password == null)
            {
                errors.Add(new ErrorItem("auth_password_required", "The password is required."));
            }
            else
            {
                errors.AddRange(new PasswordValidator().Validate(value.Password));
            }

            return errors;
        }
    }
}
