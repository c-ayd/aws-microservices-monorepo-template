using AuthService.Application.Validations.Constraints;
using Common.Http.Response.Structures;

namespace AuthService.Application.Errors
{
    public static class ServiceErrors
    {
        public static readonly ErrorItem WrongCredentialsError = new ErrorItem("auth_wrong_credentials", "The credentials are wrong.");
        public static readonly ErrorItem EmailRequiredError = new ErrorItem("auth_email_required", "The email address is required.");
        public static readonly ErrorItem EmailInUseError = new ErrorItem("auth_email_in_use", "The email address is already in use.");
        public static readonly ErrorItem InvalidEmailError = new ErrorItem("auth_error_email_invalid", "The email address is invalid.");
        public static readonly ErrorItem EmailMaxLengthError = new ErrorItem("auth_error_email_max_length", $"The email address cannot be longer than {AccountConstraints.EmailMaxLength} characters.");
        public static readonly ErrorItem WrongPasswordError = new ErrorItem("auth_password_wrong", "The password is wrong.");
        public static readonly ErrorItem PasswordRequiredError = new ErrorItem("auth_password_required", "The password is required.");
        public static readonly ErrorItem PasswordLengthError = new ErrorItem("auth_error_password_length", $"The password must be between {AccountConstraints.PasswordMinLength} and {AccountConstraints.PasswordMaxLength} characters.");
        public static readonly ErrorItem PasswordFormatError = new ErrorItem("auth_error_password_format", "The password must constain at least one lowercase, one uppercase, one digit and one special character.");

        public static readonly ErrorItem AccountNotFound = new ErrorItem("auth_account_not_found", "The account could not be found.");
        public static readonly ErrorItem AccountBannedError = new ErrorItem("auth_account_banned", "The account is banned.");
        public static readonly ErrorItem AccountLockedError = new ErrorItem("auth_account_locked", "The account is locked.");
        public static readonly ErrorItem LoginLockedError = new ErrorItem("auth_account_login_locked", "The account is locked due to failed login attemps.");

        public static readonly ErrorItem TokenNotFound = new ErrorItem("auth_token_not_found", "The tokn could not be found.");
    }
}
