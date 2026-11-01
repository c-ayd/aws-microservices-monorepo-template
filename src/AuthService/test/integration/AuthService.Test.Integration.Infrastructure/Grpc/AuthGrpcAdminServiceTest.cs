using System.Security.Claims;
using AuthService.Test.Integration.Infrastructure.Collections;
using Common.Http.Authentication.Constants;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Shared.Grpc.Auth.Admin;

namespace AuthService.Test.Integration.Infrastructure.Grpc
{
    [Collection(nameof(AuthApiCollection))]
    public partial class AuthGrpcAdminServiceTest
    {
        private readonly AuthApiCollectionCluster _collectionCluster;

        public AuthGrpcAdminServiceTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        // The following test cases are created to test if the Admin policy is in place.
        [Fact]
        public async Task GetAccount_WhenNotAuthenticated_ShouldReturnUnauthorized()
        {
            // Arrange
            await using var scope = _collectionCluster.AuthApiWebApp.GetScopedService<AuthGrpcAdmin.AuthGrpcAdminClient>(out var client);

            // Act
            var exception = await Record.ExceptionAsync(async () =>
            {
                await client.GetAccountAsync(new GetAccountRequest()
                {
                    Id = Guid.NewGuid().ToString()
                });
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<RpcException>(exception);
            Assert.Equal(StatusCode.Unauthenticated, ((RpcException)exception).StatusCode);
        }

        [Fact]
        public async Task GetAccount_WhenAuthenticatedWithoutAdminRole_ShouldReturnPermissionDenied()
        {
            // Arrange
            var claims = new List<Claim>()
            {
                new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, Guid.NewGuid().ToString()),
                new Claim(ApiGatewayAuthKeys.Claims.Roles.ClaimType, "TestRole")
            };
            var httpContext = new DefaultHttpContext()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiGatewayAuthKeys.AuthenticationScheme))
            };

            var httpContextAccessor = _collectionCluster.AuthApiWebApp.GetService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = httpContext;

            await using var scope = _collectionCluster.AuthApiWebApp.GetScopedService<AuthGrpcAdmin.AuthGrpcAdminClient>(out var client);

            // Act
            var exception = await Record.ExceptionAsync(async () =>
            {
                await client.GetAccountAsync(new GetAccountRequest()
                {
                    Id = Guid.NewGuid().ToString()
                });
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<RpcException>(exception);
            Assert.Equal(StatusCode.PermissionDenied, ((RpcException)exception).StatusCode);
        }
    }
}
