using AuthService.Application.Errors;
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
                errors.Add(ServiceErrors.PasswordLengthError);

                return errors;
            }

            if (!value.Any(c => char.IsLower(c)) ||
                !value.Any(c => char.IsUpper(c)) ||
                !value.Any(c => char.IsDigit(c)) ||
                !value.Any(c => AccountConstraints.PasswordSpecialCharacters.Contains(c)))
            {
                errors.Add(ServiceErrors.PasswordFormatError);

                return errors;
            }

            return errors;
        }
    }
}
