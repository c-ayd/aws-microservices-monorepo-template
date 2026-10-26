using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response.Middlewares;
using Shared.Test.Helpers.Fixtures;
using Shared.Test.Integration.Http.Collections;

namespace Shared.Test.Integration.Http.Response.Middlewares
{
    [Collection(nameof(TestHostCollection))]
    public class AuthErrorResponseMiddlewareTest
    {
        private const string _roleName = "TestRole";

        private readonly TestHostCollectionCluster _collectionCluster;

        public AuthErrorResponseMiddlewareTest(TestHostCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

#pragma warning disable xUnit1013 // Public method should be marked as test
        public static void ConfigureApp(IApplicationBuilder app)
        {
            app.UseMiddleware<AuthErrorResponseMiddleware>();
        }

        public static void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/auth-error/anonymous", () => Results.Ok());
            endpoints.MapGet("/auth-error/authorized", () => Results.Ok())
                .RequireAuthorization();
            endpoints.MapGet("/auth-error/forbidden", () => Results.Ok())
                .RequireAuthorization(policy => policy.RequireRole(_roleName));
        }
#pragma warning restore xUnit1013 // Public method should be marked as test

        [Fact]
        public async Task Invoke_WhenResponseIs401_ShouldReturn401ErrorItem()
        {
            // Arrange
            using var client = _collectionCluster.WebApiFixture.CreateHttpClient(WebApiFixture.HttpsPortConfigName);
            
            // Act
            var responseAnonymous = await client.GetAsync("/auth-error/anonymous");
            var responseAuthorized = await client.GetAsync("/auth-error/authorized");
            var responseForbidden = await client.GetAsync("/auth-error/forbidden");

            // Assert
            Assert.Equal(HttpStatusCode.OK, responseAnonymous.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, responseAuthorized.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, responseForbidden.StatusCode);
        }

        [Fact]
        public async Task Invoke_WhenResponseIs403_ShouldReturn403ErrorItem()
        {
            // Arrange
            using var client = _collectionCluster.WebApiFixture.CreateHttpClient(WebApiFixture.HttpsPortConfigName);

            var userId = Guid.NewGuid().ToString();
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.Id.HeaderKey, userId);

            // Act
            var responseAnonymous = await client.GetAsync("/auth-error/anonymous");
            var responseAuthorized = await client.GetAsync("/auth-error/authorized");
            var responseForbidden = await client.GetAsync("/auth-error/forbidden");

            // Assert
            client.DefaultRequestHeaders.Remove(ApiGatewayAuthKeys.Claims.Id.HeaderKey);
            client.DefaultRequestHeaders.Remove(ApiGatewayAuthKeys.Claims.Roles.HeaderKey);

            Assert.Equal(HttpStatusCode.OK, responseAnonymous.StatusCode);
            Assert.Equal(HttpStatusCode.OK, responseAuthorized.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, responseForbidden.StatusCode);
        }

        [Fact]
        public async Task Invoke_WhenResponseIsNotRelatedToAuth_ShouldDoNothing()
        {
            // Arrange
            using var client = _collectionCluster.WebApiFixture.CreateHttpClient(WebApiFixture.HttpsPortConfigName);

            var userId = Guid.NewGuid().ToString();
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.Id.HeaderKey, userId);
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.Roles.HeaderKey, _roleName);

            // Act
            var responseAnonymous = await client.GetAsync("/auth-error/anonymous");
            var responseAuthorized = await client.GetAsync("/auth-error/authorized");
            var responseForbidden = await client.GetAsync("/auth-error/forbidden");

            // Assert
            client.DefaultRequestHeaders.Remove(ApiGatewayAuthKeys.Claims.Id.HeaderKey);
            client.DefaultRequestHeaders.Remove(ApiGatewayAuthKeys.Claims.Roles.HeaderKey);
            
            Assert.Equal(HttpStatusCode.OK, responseAnonymous.StatusCode);
            Assert.Equal(HttpStatusCode.OK, responseAuthorized.StatusCode);
            Assert.Equal(HttpStatusCode.OK, responseForbidden.StatusCode);
        }
    }
}
