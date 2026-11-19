using System.Net;
using System.Reflection;
using ApiGateway.Test.Integration.Web.Collections;
using ApiGateway.Web.Middlewares;
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
        public async Task Invoke_WhenAuthenticatedWithAdminRoleAndExceedsLimit_ShouldReturnTooManyRequests()
        {
            // Arrange
            var limit = (int)typeof(RateLimiterMiddleware)
                .GetField("_adminLimit", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
            
            var authServiceClient = _collectionCluster.AuthServiceFixture.CreateHttpClient();
            var tokenResponse = await authServiceClient.GetAsync($"/test/generate-access-token?accountId={Guid.NewGuid()}&roles=Admin");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClient();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);
            apiGatewayClient.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.1");

            // Act
            var requests = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < limit; ++i)
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
        public async Task Invoke_WhenAuthenticatedAndExceedsLimit_ShouldReturnTooManyRequests()
        {
            // Arrange
            var limit = (int)typeof(RateLimiterMiddleware)
                .GetField("_authenticatedLimit", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
            
            var authServiceClient = _collectionCluster.AuthServiceFixture.CreateHttpClient();
            var tokenResponse = await authServiceClient.GetAsync($"/test/generate-access-token?accountId={Guid.NewGuid()}");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClient();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);
            apiGatewayClient.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.2");

            // Act
            var requests = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < limit; ++i)
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
        public async Task Invoke_WhenNotAuthenticatedAndExceedsLimit_ShouldReturnTooManyRequests()
        {
            // Arrange
            var limit = (int)typeof(RateLimiterMiddleware)
                .GetField("_notAuthenticatedLimit", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClient();
            apiGatewayClient.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.3");

            // Act
            var requests = new List<Task<HttpResponseMessage>>();
            for (int i = 0; i < limit; ++i)
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
        public async Task Invoke_WhenRedisIsDown_ShouldAllowTrafficUntilRedisIsUp()
        {
            // Arrange
            var authServiceClient = _collectionCluster.AuthServiceFixture.CreateHttpClient();
            var tokenResponse = await authServiceClient.GetAsync($"/test/generate-access-token?accountId={Guid.NewGuid()}");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClient();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);
            apiGatewayClient.DefaultRequestHeaders.Add("X-Forwarded-For", "192.168.1.4");

            // Act
            await _collectionCluster.RateLimiterRedis.BlockTrafficAsync();

            HttpResponseMessage responseRedisDown;
            try
            {
                responseRedisDown = await apiGatewayClient.GetAsync("/auth");
            }
            finally
            {
                await _collectionCluster.RateLimiterRedis.UnblockTrafficAsync();
            }
            
            var responseRedisUp = await apiGatewayClient.GetAsync("/auth");

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, responseRedisDown.StatusCode);
            Assert.Equal(HttpStatusCode.NoContent, responseRedisUp.StatusCode);
        }
    }
}
