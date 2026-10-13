using Shared.AspNetCore.Helpers.Options;

namespace AuthService.Application.Options
{
    public class TokenLifespansOptions : IOptions
    {
        public static string Key => "TokenLifespans";

        public required int EmailVerificationLifespanInHours { get; set; }
        public required int ResetPasswordLifespanInMinutes { get; set; }
    }
}
