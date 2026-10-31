using System.Security.Claims;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Microsoft.AspNetCore.Http;
using Common.Http.Authentication.Constants;

namespace Common.Grpc.Helpers.Interceptors
{
    public class GrpcClientAuthInterceptor : Interceptor
    {
        private readonly IHttpContextAccessor _httpContextAccessor;

        public GrpcClientAuthInterceptor(IHttpContextAccessor httpContextAccessor)
        {
            _httpContextAccessor = httpContextAccessor;
        }

        private ClientInterceptorContext<TRequest, TResponse> GetContextWithAuthHeaders<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context)
            where TRequest : class
            where TResponse : class
        {
            var user = _httpContextAccessor.HttpContext?.User;
            if (user == null || user.Identity == null || !user.Identity.IsAuthenticated)
                return context;

            var metadata = new Metadata();
            if (context.Options.Headers != null)
            {
                foreach (var header in context.Options.Headers)
                {
                    metadata.Add(header);
                }
            }

            var accountId = user.FindFirstValue(ApiGatewayAuthKeys.Claims.Id.ClaimType);
            if (string.IsNullOrEmpty(accountId))
                return context;

            foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
            {
                if (userClaim.IsMultiple)
                {
                    var claimValues = user.FindAll(userClaim.ClaimType)
                        .Select(c => c.Value)
                        .ToArray();

                    if (claimValues.Length == 0)
                        continue;

                    foreach (var claimValue in claimValues)
                    {
                        metadata.Add(userClaim.HeaderKey, claimValue);
                    }
                }
                else
                {
                    var claimValue = user.FindFirstValue(userClaim.ClaimType);
                    if (string.IsNullOrEmpty(claimValue))
                        continue;

                    metadata.Add(userClaim.HeaderKey, claimValue);
                }
            }

            return new ClientInterceptorContext<TRequest, TResponse>(
                context.Method,
                context.Host,
                context.Options.WithHeaders(metadata));
        }

        public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(request, GetContextWithAuthHeaders(context));
        }

        public override AsyncClientStreamingCall<TRequest, TResponse> AsyncClientStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncClientStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(GetContextWithAuthHeaders(context));
        }

        public override AsyncDuplexStreamingCall<TRequest, TResponse> AsyncDuplexStreamingCall<TRequest, TResponse>(
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncDuplexStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(GetContextWithAuthHeaders(context));
        }

        public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(
            TRequest request,
            ClientInterceptorContext<TRequest, TResponse> context,
            AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
        {
            return continuation(request, GetContextWithAuthHeaders(context));
        }
    }
}
