using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Shared.Http.Authentication.Constants;

namespace Shared.Logging.Middlewares
{
    public class AuthScopeLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<AuthScopeLoggingMiddleware> _logger;

        public AuthScopeLoggingMiddleware(
            RequestDelegate next,
            ILogger<AuthScopeLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            string accountId;
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                accountId = context.User.Identity.Name!;
            }
            else
            {
                accountId = "Not authenticated";
            }

            using (var scope = _logger.BeginScope("Account ID: {AccountId}",
                accountId))
            {
                await _next(context);
            }
        }
    }
}
