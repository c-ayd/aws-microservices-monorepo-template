using System.Net;
using System.Net.Http.Json;
using ApiGateway.Test.Integration.Web.Collections;
using Shared.Http.Authentication.Constants;
using Shared.Test.Generators;

namespace ApiGateway.Test.Integration.Web.Transforms.Request
{
    [Collection(nameof(ApiGatewayCollection))]
    public class ClearReservedHeadersTransformTest
    {
        private readonly ApiGatewayCollectionCluster _collectionCluster;

        public ClearReservedHeadersTransformTest(ApiGatewayCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task ApplyTransform_WhenNotAuthenticated_ShouldAddNothingToHeaders()
        {
            // Arrange
            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClient();
            foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
            {
                apiGatewayClient.DefaultRequestHeaders.Add(userClaim.HeaderKey, StringGenerator.GenerateAlpha());
            }

            // Act
            var response = await apiGatewayClient.GetAsync("/auth/test/get-headers");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var headers = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
            {
                Assert.Null(headers![userClaim.HeaderKey]);
            }
        }
    }
}
