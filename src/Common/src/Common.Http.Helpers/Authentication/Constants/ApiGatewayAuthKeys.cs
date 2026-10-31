using System.Reflection;
using Common.Http.Authentication.Structures;

namespace Common.Http.Authentication.Constants
{
    public static class ApiGatewayAuthKeys
    {
        public const string AuthenticationScheme = "API-Gateway";

        public static class Claims
        {
            public static readonly UserClaim Id = new UserClaim(JwtClaimTypes.Id, "X-User-Id", IsMultiple: false);
            public static readonly UserClaim Roles = new UserClaim(JwtClaimTypes.Role, "X-User-Roles", IsMultiple: true);
            public static readonly UserClaim EmailVerified = new UserClaim(JwtClaimTypes.EmailVerified, "X-User-Email-Verified", IsMultiple: false);
            public static readonly UserClaim PreferredLanguage = new UserClaim(JwtClaimTypes.PreferredLanguage, "X-User-Preferred-Language", IsMultiple: false);
            public static readonly UserClaim IssuedAt = new UserClaim(JwtClaimTypes.IssuedAt, "X-User-Issued-At", IsMultiple: false);

            public static readonly IReadOnlyList<UserClaim> AllUserClaims = typeof(ApiGatewayAuthKeys.Claims)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(f => f.FieldType == typeof(UserClaim))
                .Select(f => (UserClaim)f.GetValue(null)!)
                .ToList();
        }
    }
}
