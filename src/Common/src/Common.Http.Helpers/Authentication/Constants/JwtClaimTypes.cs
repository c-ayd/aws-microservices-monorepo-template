using System.IdentityModel.Tokens.Jwt;

namespace Common.Http.Authentication.Constants
{
    /// <summary>
    /// Centralizes and provides JWT claim types in the application
    /// </summary>
    public static class JwtClaimTypes
    {
        // Pre-defined
        public const string Id = JwtRegisteredClaimNames.Sub;
        public const string EmailVerified = JwtRegisteredClaimNames.EmailVerified;
        public const string IssuedAt = JwtRegisteredClaimNames.Iat;

        // Custom
        public const string Role = "role";
        public const string PreferredLanguage = "preferred_language";
    }
}
