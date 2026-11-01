using Common.Http.Response.Structures;

namespace ApiGateway.Web.Errors
{
    public static class ServiceErrors
    {
        public static readonly ErrorItem RateLimitExceededError = new ErrorItem("api_gateway_rate_limiter", "The rate limit has been reached.");
    }
}
