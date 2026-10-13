using System.Net;
using ApiGateway.Web.DistributedCaches;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response;
using Shared.Http.Response.Structures;

namespace ApiGateway.Web.Middlewares
{
    public class RateLimiterMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly RateLimiterRedis _rateLimiterRedis;

        public RateLimiterMiddleware(
            RequestDelegate next,
            RateLimiterRedis rateLimiterRedis)
        {
            _next = next;
            _rateLimiterRedis = rateLimiterRedis;
        }

        public async Task Invoke(HttpContext context)
        {
            bool isAuthenticated = context.User.Identity != null && context.User.Identity.IsAuthenticated;
            bool isAdmin = isAuthenticated &&
                context.User.FindAll(c => c.Type == ApiGatewayAuthKeys.Claims.Roles.ClaimType).FirstOrDefault(c => c.Value == "Admin") != null;

            string key;
            int limit;
            TimeSpan window;
            if (isAdmin)
            {
                key = $"admin:{context.User.Identity!.Name}";
                limit = 300;
                window = TimeSpan.FromMinutes(1);
            }
            else if (isAuthenticated)
            {
                key = $"auth:{context.User.Identity!.Name}";
                limit = 100;
                window = TimeSpan.FromMinutes(1);
            }
            else
            {
                key = $"not-auth:{context.Connection.RemoteIpAddress?.ToString() ?? "unknown"}";
                limit = 30;
                window = TimeSpan.FromMinutes(1);
            }

            var (isAllowed, cooldownTimeInSeconds) = await _rateLimiterRedis.CheckRateLimitAsync(key, limit, window);
            if (isAllowed)
            {
                await _next(context);
            }
            else
            {
                await JsonResponseBuilder.Error(
                    HttpStatusCode.TooManyRequests,
                    [
                        new ErrorItem("api_gateway_rate_limiter", "The rate limit has been reached.")
                    ],
                    new
                    {
                        cooldownTimeInSeconds
                    }
                ).ExecuteAsync(context);
            }
        }
    }
}
