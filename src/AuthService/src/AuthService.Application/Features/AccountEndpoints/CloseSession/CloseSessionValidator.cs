using AuthService.Application.Errors;
using AuthService.Application.Validations.Shared;
using Common.Http.Response.Structures;
using Common.Http.Validation;

namespace AuthService.Application.Features.AccountEndpoints.CloseSession
{
    public class CloseSessionValidator : IValidator<CloseSessionRequest>
    {
        public List<ErrorItem> Validate(CloseSessionRequest value)
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
