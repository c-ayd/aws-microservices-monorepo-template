using System.Net;
using ApiGateway.Test.Integration.Web.Collections;
using Microsoft.Net.Http.Headers;

namespace ApiGateway.Test.Integration.Web
{
    [Collection(nameof(ApiGatewayCollection))]
    public class ProgramTest
    {
        private readonly ApiGatewayCollectionCluster _collectionCluster;

        public ProgramTest(ApiGatewayCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task Proxy_WhenNotAuthenticatedAndProtectedServiceIsRequested_ShouldReturnUnauthorized()
        {
            // Arrange
            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpsClient();

            // Act
            var response = await apiGatewayClient.GetAsync("/protected");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Proxy_WhenAuthenticatedAndProtectedServiceIsRequested_ShouldReturnNoContent()
        {
            // Arrange
            var authServiceClient = _collectionCluster.AuthServiceFixture.CreateHttpsClient();
            var tokenResponse = await authServiceClient.GetAsync($"/test/generate-access-token?accountId={Guid.NewGuid().ToString()}");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpsClient();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);

            // Act
            var response = await apiGatewayClient.GetAsync("/protected");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task Proxy_WhenNotAuthenticatedAndAdminServiceIsRequested_ShouldReturnUnauthorized()
        {
            // Arrange
            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpsClient();

            // Act
            var response = await apiGatewayClient.GetAsync("/admin");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Proxy_WhenAuthenticatedWithNoAdminRoleAndAdminServiceIsRequested_ShouldReturnForbidden()
        {
            // Arrange
            var authServiceClient = _collectionCluster.AuthServiceFixture.CreateHttpsClient();
            var tokenResponse = await authServiceClient.GetAsync($"/test/generate-access-token?accountId={Guid.NewGuid()}");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpsClient();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);

            // Act
            var response = await apiGatewayClient.GetAsync("/admin");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Proxy_WhenAuthenticatedWithAdminRoleAndAdminServiceIsRequested_ShouldReturnNoContent()
        {
            // Arrange
            var authServiceClient = _collectionCluster.AuthServiceFixture.CreateHttpsClient();
            var tokenResponse = await authServiceClient.GetAsync($"/test/generate-access-token?accountId={Guid.NewGuid()}&roles=Admin");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpsClient();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);

            // Act
            var response = await apiGatewayClient.GetAsync("/admin");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }
    }
}
