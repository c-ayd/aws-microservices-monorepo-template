using System.Net;
using Common.Http.Helpers.Response;
using Common.Http.Response;
using Common.Http.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Common.Http.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static IEndpointConventionBuilder AddValidation<T>(this IEndpointConventionBuilder builder)
        {
            return builder.AddEndpointFilter(async (context, next) =>
            {
                var request = context.Arguments.OfType<T>().FirstOrDefault();
                if (request == null)
                    return JsonResponseBuilder.Error(
                        HttpStatusCode.BadRequest,
                        errors: [
                            CommonErrors.RequestBodyMissingError
                        ]
                    );

                var errors = context.HttpContext.RequestServices
                    .GetRequiredService<IValidator<T>>()
                    .Validate(request);
                
                if (errors.Count > 0)
                    return JsonResponseBuilder.Error(
                        HttpStatusCode.BadRequest,
                        errors
                    );

                return await next(context);
            });
        }
        
        public static IEndpointConventionBuilder AddAsyncValidation<T>(this IEndpointConventionBuilder builder)
        {
            return builder.AddEndpointFilter(async (context, next) =>
            {
                var request = context.Arguments.OfType<T>().FirstOrDefault();
                if (request == null)
                    return JsonResponseBuilder.Error(
                        HttpStatusCode.BadRequest,
                        errors: [
                            CommonErrors.RequestBodyMissingError
                        ]
                    );

                var errors = await context.HttpContext.RequestServices
                    .GetRequiredService<IAsyncValidator<T>>()
                    .ValidateAsync(request, context.HttpContext.RequestAborted);
                
                if (errors.Count > 0)
                    return JsonResponseBuilder.Error(
                        HttpStatusCode.BadRequest,
                        errors
                    );

                return await next(context);
            });
        }
    }
}
