using System.Net;
using ApiGateway.Test.Integration.Web.Collections;
using Microsoft.Net.Http.Headers;

namespace ApiGateway.Test.Integration.Web
{
    [Collection(nameof(ApiGatewayCollection))]
    public class RateLimiterMiddlewareTest
    {
        private readonly ApiGatewayCollectionCluster _collectionCluster;

        public RateLimiterMiddlewareTest(ApiGatewayCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task RateLimiter_WhenAuthenticatedAndExceedsLimit_ShouldReturnTooManyRequests()
        {
            // Arrange
            var authServiceClient = _collectionCluster.AuthServiceFixture.CreateHttpsClient();
            var tokenResponse = await authServiceClient.GetAsync($"/test/generate-access-token?accountId={Guid.NewGuid().ToString()}");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpsClient();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);

            // Act
            var requests = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 100; ++i)
            {
                requests.Add(apiGatewayClient.GetAsync("/auth"));
            }

            var responses = await Task.WhenAll(requests);
            var rejectedResponse = await apiGatewayClient.GetAsync("/auth");

            // Assert
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        }

        [Fact]
        public async Task RateLimiter_WhenAuthenticatedWithAdminRoleAndExceedsLimit_ShouldReturnTooManyRequests()
        {
            // Arrange
            var authServiceClient = _collectionCluster.AuthServiceFixture.CreateHttpsClient();
            var tokenResponse = await authServiceClient.GetAsync($"/test/generate-access-token?accountId={Guid.NewGuid()}&roles=Admin");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpsClient();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);

            // Act
            var requests = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < 300; ++i)
            {
                requests.Add(apiGatewayClient.GetAsync("/auth"));
            }

            var responses = await Task.WhenAll(requests);
            var rejectedResponse = await apiGatewayClient.GetAsync("/auth");

            // Assert
            foreach (var response in responses)
            {
                Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            }

            Assert.Equal(HttpStatusCode.TooManyRequests, rejectedResponse.StatusCode);
        }
    }
}
