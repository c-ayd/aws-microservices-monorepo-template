using System.Net;
using Common.Http.Helpers.Response;
using Microsoft.AspNetCore.Http;

namespace Common.Http.Response.Middlewares
{
    public class AuthErrorResponseMiddleware
    {
        private readonly RequestDelegate _next;

        public AuthErrorResponseMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task Invoke(HttpContext context)
        {
            await _next(context);

            switch (context.Response.StatusCode)
            {
                case StatusCodes.Status401Unauthorized:
                    await JsonResponseBuilder.Error(
                        HttpStatusCode.Unauthorized,
                        errors: [
                            CommonErrors.UnauthorizedError
                        ]
                    ).ExecuteAsync(context);
                    break;
                case StatusCodes.Status403Forbidden:
                    await JsonResponseBuilder.Error(
                        HttpStatusCode.Forbidden,
                        errors: [
                            CommonErrors.ForbiddenError
                        ]
                    ).ExecuteAsync(context);
                    break;
                default:
                    break;
            }
        }
    }
}
