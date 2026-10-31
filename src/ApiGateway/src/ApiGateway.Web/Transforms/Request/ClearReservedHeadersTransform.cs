using Common.Http.Authentication.Constants;
using Yarp.ReverseProxy.Transforms;
using Yarp.ReverseProxy.Transforms.Builder;

namespace ApiGateway.Web.Transforms.Request
{
    public class ClearReservedHeadersTransform : ITransformProvider
    {
        public void Apply(TransformBuilderContext context)
        {
            context.AddRequestTransform(ApplyTransform);
        }

        private ValueTask ApplyTransform(RequestTransformContext transformContext)
        {
            ClearReservedHeaders(transformContext);
            return ValueTask.CompletedTask;
        }

        private void ClearReservedHeaders(RequestTransformContext transformContext)
        {
            // In case the reserved headers for the downstream services are set by the client and
            // these headers are not set or cleared correctly during transformations, the reserved
            // headers are cleared before any tranform starts.

            foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
            {
                transformContext.ProxyRequest.Headers.Remove(userClaim.HeaderKey);
            }
        }

        public void ValidateCluster(TransformClusterValidationContext context)
        {
        }

        public void ValidateRoute(TransformRouteValidationContext context)
        {
        }
    }
}
