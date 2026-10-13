using ApiGateway.Test.Integration.Web.Collections;
using Microsoft.Extensions.Configuration;
using Microsoft.Net.Http.Headers;

namespace ApiGateway.Test.Integration.Web
{
    [Collection(nameof(ApiGatewayCollection))]
    public class CorsTest
    {
        private readonly ApiGatewayCollectionCluster _collectionCluster;

        public CorsTest(ApiGatewayCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task Proxy_WhenRequestIsMadeFromCorsOrigins_ShouldReturnWithProperHeader()
        {
            // Arrange
            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClient("HttpsOnly");
            var origin = _collectionCluster.ApiGatewayWebApp.GetService<IConfiguration>().GetValue<string>("CorsOrigins:AngularWeb");
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Origin, origin);
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.AccessControlRequestMethod, "GET");

            // Act
            var response = await apiGatewayClient.GetAsync("/auth");

            // Assert
            Assert.True(response.Headers.Contains(HeaderNames.AccessControlAllowOrigin));
        }

        [Fact]
        public async Task Proxy_WhenRequestIsNotMadeFromCorsOrigins_ShouldReturnWithMissingHeader()
        {
            // Arrange
            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClient("HttpsOnly");
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Origin, "http://localhost:8080");
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.AccessControlRequestMethod, "GET");

            // Act
            var response = await apiGatewayClient.GetAsync("/auth");

            // Assert
            Assert.False(response.Headers.Contains(HeaderNames.AccessControlAllowOrigin));
        }
    }
}
