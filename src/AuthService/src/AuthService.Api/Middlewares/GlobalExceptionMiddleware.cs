using System.Net;
using Common.Http.Response;
using Common.Http.Response.Structures;

namespace AuthService.Api.Middlewares
{
    public class GlobalExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<GlobalExceptionMiddleware> _logger;

        public GlobalExceptionMiddleware(
            RequestDelegate next,
            ILogger<GlobalExceptionMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            try
            {
                await _next(context);
            }
            catch (BadHttpRequestException)
            {
                if (!context.Response.HasStarted)
                {
                    await JsonResponseBuilder.Error(
                        HttpStatusCode.BadRequest,
                        [
                            new ErrorItem("route_or_query_params_wrong", "The given route or query parameters are wrong.")
                        ]
                    ).ExecuteAsync(context);
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong. Message: {Message}",
                    exception.Message);

                if (!context.Response.HasStarted)
                {
                    await JsonResponseBuilder.Error(
                        HttpStatusCode.InternalServerError,
                        [
                            new ErrorItem("internal_server_error", "Something went wrong.")
                        ]
                    ).ExecuteAsync(context);
                }
            }
        }
    }
}
