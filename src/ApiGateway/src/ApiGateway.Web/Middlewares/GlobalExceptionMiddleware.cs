using System.Net;
using Common.Constants.Errors;
using Common.Http.Response;

namespace ApiGateway.Web.Middlewares
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
                        errors: [
                            CommonErrors.BindingError
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
                        errors: [
                            CommonErrors.UnknownError
                        ]
                    ).ExecuteAsync(context);
                }
            }
        }
    }
}
