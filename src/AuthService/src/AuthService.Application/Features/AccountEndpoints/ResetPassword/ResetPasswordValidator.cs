using AuthService.Application.Errors;
using AuthService.Application.Validations.Shared;
using Common.Http.Response.Structures;
using Common.Http.Validation;

namespace AuthService.Application.Features.AccountEndpoints.ResetPassword
{
    public class ResetPasswordValidator : IValidator<ResetPasswordRequest>
    {
        public List<ErrorItem> Validate(ResetPasswordRequest value)
        {
            var errors = new List<ErrorItem>();

            if (value.Password == null)
            {
                errors.Add(ServiceErrors.PasswordRequiredError);
            }
            else
            {
                errors.AddRange(new PasswordValidator().Validate(value.Password));
            }

            return errors;
        }
    }
}
