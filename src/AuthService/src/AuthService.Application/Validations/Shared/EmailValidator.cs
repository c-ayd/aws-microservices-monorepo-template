using System.Net.Mail;
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
                errors.Add(new ErrorItem("auth_error_email_invalid", "The email address is invalid."));
                
                return errors;
            }

            if (value.Length > AccountConstraints.EmailMaxLength)
            {
                errors.Add(new ErrorItem("auth_error_email_max_length", $"The email address cannot be longer than {AccountConstraints.EmailMaxLength} characters.",
                    Metadata: new
                    {
                        MaxLength = AccountConstraints.EmailMaxLength
                    }
                ));

                return errors;
            }

            return errors;
        }
    }
}
