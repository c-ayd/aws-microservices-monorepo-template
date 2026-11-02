using System.Net;
using AdminService.Test.Integration.Application.Collections;
using Common.Constants;
using Common.Http.Authentication.Constants;
using Common.Http.Helpers.Authentication;

namespace AdminService.Test.Integration.Application.Features.AuthEndpoints.GetAccount
{
    [Collection(nameof(AdminApiCollection))]
    public class GetAccountHandlerTest
    {
        private readonly AdminApiCollectionCluster _collectionCluster;

        public GetAccountHandlerTest(AdminApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task HandleAsync_WhenAccountIsNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var client = _collectionCluster.AdminApiWebApp.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, Guid.NewGuid().ToString() },
                    { ApiGatewayAuthKeys.Claims.Roles.HeaderKey, Policies.AdminPolicy.RoleName },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.GetAsync($"/auth/account/{Guid.Empty}");

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenAccountFound_ShouldReturnOk()
        {
            // Arrange
            var client = _collectionCluster.AdminApiWebApp.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, Guid.NewGuid().ToString() },
                    { ApiGatewayAuthKeys.Claims.Roles.HeaderKey, Policies.AdminPolicy.RoleName },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.GetAsync($"/auth/account/{Guid.NewGuid().ToString()}");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
