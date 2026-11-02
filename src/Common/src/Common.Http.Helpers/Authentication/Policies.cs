using Microsoft.AspNetCore.Authorization;

namespace Common.Http.Helpers.Authentication
{
    public static class Policies
    {
        public static class AdminPolicy
        {
            public const string PolicyName = "Admin";
            public const string RoleName = "Admin";
        }
        public static void AddAdminPolicy(this AuthorizationOptions options)
        {
            options.AddPolicy(AdminPolicy.PolicyName, policy => policy.RequireRole(AdminPolicy.RoleName));
        }

        public static class AuthenticatedPolicy
        {
            public const string PolicyName = "Authenticated";
        }
        public static void AddAuthenticatedPolicy(this AuthorizationOptions options)
        {
            options.AddPolicy(AuthenticatedPolicy.PolicyName, policy => policy.RequireAuthenticatedUser());
        }
    }
}
