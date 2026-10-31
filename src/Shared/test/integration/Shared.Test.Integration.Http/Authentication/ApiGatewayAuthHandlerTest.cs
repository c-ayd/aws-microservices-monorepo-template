using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shared.Http.Authentication;
using Shared.Http.Authentication.Constants;
using Shared.Http.Authentication.Structures;
using Shared.Test.Generators;
using Shared.Test.Integration.Http.Collections;

namespace Shared.Test.Integration.Http.Authentication
{
    [Collection(nameof(TestHostCollection))]
    public class ApiGatewayAuthHandlerTest
    {
        private const string _roleName = "TestRole";
        
        private readonly TestHostCollectionCluster _collectionCluster;

        public ApiGatewayAuthHandlerTest(TestHostCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

#pragma warning disable xUnit1013 // Public method should be marked as test
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddAuthentication(ApiGatewayAuthKeys.AuthenticationScheme)
                .AddScheme<AuthenticationSchemeOptions, ApiGatewayAuthHandler>(ApiGatewayAuthKeys.AuthenticationScheme, options => { });
            services.AddAuthorization();
        }

        public static void ConfigureApp(IApplicationBuilder app)
        {
            app.UseAuthentication();
            app.UseAuthorization();
        }

        public static void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGet("/api-gateway", (HttpContext context) => Results.Ok(new UserDto
            {
                IsAuthenticated = context.User.Identity?.IsAuthenticated,
                Name = context.User.Identity?.Name,
                Claims = context.User.Claims.Select(c => new UserDto.ClaimDto() { Type = c.Type, Value = c.Value })
            }));
            endpoints.MapGet("/api-gateway/authorized", () => Results.Ok())
                .RequireAuthorization();
            endpoints.MapGet("/api-gateway/access-granted", () => Results.Ok())
                .RequireAuthorization(policy => policy.RequireRole(_roleName));
            endpoints.MapGet("/api-gateway/forbidden", () => Results.Ok())
                .RequireAuthorization(policy => policy.RequireRole(_roleName + "a"));
        }
#pragma warning restore xUnit1013 // Public method should be marked as test

        [Fact]
        public async Task Invoke_WhenHeadersHaveUserContent_ShouldFillClaimPrincipalAndAuthorize()
        {
            // Arrange
            using var client = _collectionCluster.WebApiFixture.CreateHttpClient();

            var userId = Guid.NewGuid().ToString();
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.Id.HeaderKey, userId);
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.Roles.HeaderKey, "User");
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.Roles.HeaderKey, _roleName);

            // Act
            var responseUser = await client.GetFromJsonAsync<UserDto>("/api-gateway");
            var responseAuthorized = await client.GetAsync("/api-gateway/authorized");
            var responseAccessGranted = await client.GetAsync("/api-gateway/access-granted");
            var responseForbidden = await client.GetAsync("/api-gateway/forbidden");

            // Assert
            Assert.NotNull(responseUser);
            Assert.True(responseUser.IsAuthenticated, "The user is not authenticated.");
            Assert.Equal(userId, responseUser.Name);
            Assert.Contains(responseUser.Claims, claim => claim.Type == ApiGatewayAuthKeys.Claims.Roles.ClaimType &&
                claim.Value == "User");
            Assert.Contains(responseUser.Claims, claim => claim.Type == ApiGatewayAuthKeys.Claims.Roles.ClaimType &&
                claim.Value == _roleName);

            Assert.Equal(HttpStatusCode.OK, responseAuthorized.StatusCode);
            Assert.Equal(HttpStatusCode.OK, responseAccessGranted.StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, responseForbidden.StatusCode);
        }

        [Fact]
        public async Task Invoke_WhenHeadersHaveNoUserContent_ShouldLeftClaimPrincipalEmptyAndNotAuthorize()
        {
            // Arrange
            using var client = _collectionCluster.WebApiFixture.CreateHttpClient();

            // Act
            var responseUser = await client.GetFromJsonAsync<UserDto>("/api-gateway");
            var responseAuthorized = await client.GetAsync("/api-gateway/authorized");
            var responseAccessGranted = await client.GetAsync("/api-gateway/access-granted");
            var responseForbidden = await client.GetAsync("/api-gateway/forbidden");

            // Assert
            Assert.NotNull(responseUser);
            Assert.False(responseUser.IsAuthenticated, "The user is authenticated.");
            Assert.Null(responseUser.Name);
            Assert.Empty(responseUser.Claims);

            Assert.Equal(HttpStatusCode.Unauthorized, responseAuthorized.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, responseAccessGranted.StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, responseForbidden.StatusCode);
        }

        private class UserDto
        {
            public bool? IsAuthenticated { get; set; }
            public string? Name { get; set; }
            public required IEnumerable<ClaimDto> Claims { get; set; }

            public class ClaimDto
            {
                public required string Type { get; set; }
                public required string Value { get; set; }
            }
        }
    }
}
