using Shared.AspNetCore.Helpers.Options;

namespace AuthService.Application.Options
{
    public class JwtOptions : IOptions
    {
        public static string Key => "Jwt";

        public required string Issuer { get; set; }
        public required string Audience { get; set; }
        public required int AccessTokenLifespanInMinutes { get; set; }
        public required int RefreshTokenLifespanInDays { get; set; }
    }
}
