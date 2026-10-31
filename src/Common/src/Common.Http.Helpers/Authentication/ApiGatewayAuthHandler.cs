using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Common.Http.Authentication.Constants;

namespace Common.Http.Authentication
{
    public class ApiGatewayAuthHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public ApiGatewayAuthHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder) 
            : base(options, logger, encoder)
        {
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // Check if the user is authenticated
            if (!Request.Headers.TryGetValue(ApiGatewayAuthKeys.Claims.Id.HeaderKey, out _))
                return AuthenticateResult.NoResult();

            // Extract all claims from the headers
            var claims = new List<Claim>();
            foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
            {
                if (!Request.Headers.TryGetValue(userClaim.HeaderKey, out var headerValue))
                    continue;

                if (userClaim.IsMultiple)
                {
                    var values = headerValue.ToString()
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(h => h.Trim());

                    foreach (var value in values)
                    {
                        claims.Add(new Claim(userClaim.ClaimType, value));
                    }
                }
                else
                {
                    claims.Add(new Claim(userClaim.ClaimType, headerValue.ToString()));
                }
            }

            var identity = new ClaimsIdentity(claims, ApiGatewayAuthKeys.AuthenticationScheme,
                ApiGatewayAuthKeys.Claims.Id.ClaimType, ApiGatewayAuthKeys.Claims.Roles.ClaimType);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, ApiGatewayAuthKeys.AuthenticationScheme);
            return AuthenticateResult.Success(ticket);
        }
    }
}
