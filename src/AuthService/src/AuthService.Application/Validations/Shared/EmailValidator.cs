using System.Net.Mail;
using AuthService.Application.Errors;
using AuthService.Application.Validations.Constraints;
using Common.Http.Response.Structures;
using Common.Http.Validation;

namespace AuthService.Application.Validations.Shared
{
    public class EmailValidator : IValidator<string>
    {
        public List<ErrorItem> Validate(string value)
        {
            var errors = new List<ErrorItem>();

            if (!MailAddress.TryCreate(value, out _))
            {
                errors.Add(ServiceErrors.InvalidEmailError);
                
                return errors;
            }

            if (value.Length > AccountConstraints.EmailMaxLength)
            {
                errors.Add(ServiceErrors.EmailMaxLengthError);

                return errors;
            }

            return errors;
        }
    }
}
