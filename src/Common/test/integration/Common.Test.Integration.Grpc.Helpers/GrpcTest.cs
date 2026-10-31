using System.Net;
using System.Net.Http.Json;
using Common.Http.Authentication.Constants;
using Common.Http.Authentication.Structures;
using Common.Test.Integration.Grpc.Helpers.Collections;

namespace Common.Test.Integration.Grpc.Helpers
{
    [Collection(nameof(GrpcCollection))]
    public class GrpcTest
    {
        private readonly GrpcCollectionCluster _collectionCluster;

        public GrpcTest(GrpcCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task Anonymous_WhenNotAuthenticated_ShouldReturnOkWithGrpcResponse()
        {
            // Arrange
            var client = _collectionCluster.GrpcClientFixture.CreateHttpClient();
            
            // Act
            var response = await client.GetAsync("/anonymous");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<TestGrpcResponse>();
            Assert.Empty(body!.StrValue);
            Assert.Equal(5, body.IntValue);
        }

        [Fact]
        public async Task Authenticated_WhenNotAuthenticated_ShouldReturnUnauthorized()
        {
            // Arrange
            var client = _collectionCluster.GrpcClientFixture.CreateHttpClient();

            // Act
            var response = await client.GetAsync("/authenticated");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Authenticated_WhenAuthenticated_ShouldReturnOkWithGrpcResponse()
        {
            // Arrange
            var client = _collectionCluster.GrpcClientFixture.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, Guid.NewGuid().ToString() }
                }
            );

            // Act
            var response = await client.GetAsync("/authenticated");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<TestGrpcResponse>();
            Assert.Equal("Authenticated", body!.StrValue);
            Assert.Equal(10, body.IntValue);
        }

        [Fact]
        public async Task Admin_WhenNotAuthenticated_ShouldReturnUnauthorized()
        {
            // Arrange
            var client = _collectionCluster.GrpcClientFixture.CreateHttpClient();

            // Act
            var response = await client.GetAsync("/admin");

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Admin_WhenAuthenticatedWithoutAdminRole_ShouldReturnForbidden()
        {
            // Arrange
            var client = _collectionCluster.GrpcClientFixture.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, Guid.NewGuid().ToString() }
                }
            );

            // Act
            var response = await client.GetAsync("/admin");

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Admin_WhenAuthenticatedWithAdminRole_ShouldReturnOkWithGrpcResponse()
        {
            // Arrange
            var client = _collectionCluster.GrpcClientFixture.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, Guid.NewGuid().ToString() },
                    { ApiGatewayAuthKeys.Claims.Roles.HeaderKey, "User, Admin" }
                }
            );

            // Act
            var response = await client.GetAsync("/admin");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadFromJsonAsync<TestGrpcResponse>();
            Assert.Equal("Admin", body!.StrValue);
            Assert.Equal(15, body.IntValue);
        }

        [Fact]
        public async Task Exception_WhenRequestIsMade_ShouldReturnInternalServerError()
        {
            // Arrange
            var client = _collectionCluster.GrpcClientFixture.CreateHttpClient();

            // Act
            var response = await client.GetAsync("/exception");

            // Assert
            Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        }
    }
}
