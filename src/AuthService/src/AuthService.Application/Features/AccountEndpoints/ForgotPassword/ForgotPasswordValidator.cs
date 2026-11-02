using AuthService.Application.Errors;
using AuthService.Application.Validations.Shared;
using Common.Http.Response.Structures;
using Common.Http.Validation;

namespace AuthService.Application.Features.AccountEndpoints.ForgotPassword
{
    public class ForgotPasswordValidator : IValidator<ForgotPasswordRequest>
    {
        public List<ErrorItem> Validate(ForgotPasswordRequest value)
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

            return errors;
        }
    }
}
