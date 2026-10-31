using AuthService.Application.Validations.Constraints;
using Common.Http.Response.Structures;
using Common.Http.Validation;

namespace AuthService.Application.Validations.Shared
{
    public class PasswordValidator : IValidator<string>
    {
        public List<ErrorItem> Validate(string value)
        {
            var errors = new List<ErrorItem>();

            if (value.Length < AccountConstraints.PasswordMinLength ||
                value.Length > AccountConstraints.PasswordMaxLength)
            {
                errors.Add(new ErrorItem("auth_error_password_length", $"The password must be between {AccountConstraints.PasswordMinLength} and {AccountConstraints.PasswordMaxLength} characters.",
                    Metadata: new
                    {
                        MinLength = AccountConstraints.PasswordMinLength,
                        MaxLength = AccountConstraints.PasswordMaxLength
                    }
                ));

                return errors;
            }

            if (!value.Any(c => char.IsLower(c)) ||
                !value.Any(c => char.IsUpper(c)) ||
                !value.Any(c => char.IsDigit(c)) ||
                !value.Any(c => AccountConstraints.PasswordSpecialCharacters.Contains(c)))
            {
                errors.Add(new ErrorItem("auth_error_password_format", "The password must constain at least one lowercase, one uppercase, one digit and one special character.",
                    Metadata: new
                    {
                        SpecialCharacters = AccountConstraints.PasswordSpecialCharacters
                    }
                ));

                return errors;
            }

            return errors;
        }
    }
}
