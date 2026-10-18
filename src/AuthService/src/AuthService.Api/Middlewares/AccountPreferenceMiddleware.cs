using System.Security.Claims;
using AuthService.Application.Validations.Constraints;
using Shared.Constants;
using Shared.Http.Authentication.Constants;

namespace AuthService.Api.Middlewares
{
    public class AccountPreferenceMiddleware
    {
        private readonly RequestDelegate _next;

        public AccountPreferenceMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            SetPreferredLanguage(context);

            await _next(context);
        }

        private void SetPreferredLanguage(HttpContext context)
        {
            string? preferredLanguage = null;
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                preferredLanguage = context.User.FindFirstValue(ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType);
            }
            else
            {
                preferredLanguage = context.Request.GetTypedHeaders().AcceptLanguage
                    .OrderByDescending(h => h.Quality ?? 1.0)
                    .Select(h => h.Value.ToString().Split('-')[0].ToLower())
                    .FirstOrDefault();
            }

            context.Items["PreferredLanguage"] = SupportedLanguages.CheckAndGetValidLanguage(preferredLanguage);
        }
    }
}
