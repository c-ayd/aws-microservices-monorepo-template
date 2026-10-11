using System.Net;
using System.Net.Http.Json;
using ApiGateway.Test.Integration.Web.Collections;
using Microsoft.Net.Http.Headers;
using Shared.Http.Authentication.Constants;
using Shared.Redis.Extensions;

namespace ApiGateway.Test.Integration.Web.Transforms.Request
{
    [Collection(nameof(ApiGatewayCollection))]
    public class JwtBearerTransformTest
    {
        private readonly ApiGatewayCollectionCluster _collectionCluster;

        public JwtBearerTransformTest(ApiGatewayCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task ApplyTransform_WhenNotAuthenticated_ShouldAddNothingToHeaders()
        {
            // Arrange
            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClientWithTls();

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

        [Fact]
        public async Task ApplyTransform_WhenTokenIsInBlacklist_ShouldAddNothingToHeadersAndRemoveTokenFromHeaders()
        {
            // Arrange
            var accountId = Guid.NewGuid().ToString();

            var downstreamServiceClient = _collectionCluster.DownstreamServiceFixture.CreateHttpClient();
            var tokenResponse = await downstreamServiceClient.GetAsync($"/test/generate-access-token?accountId={accountId}&issuedInPast=true");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            await _collectionCluster.TokenBlacklistRedisFixture
                .GetDatabase()
                .SaveAsStringAsync(accountId, DateTimeOffset.UtcNow.ToUnixTimeSeconds(), TimeSpan.FromMinutes(5));

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClientWithTls();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);

            // Act
            var response = await apiGatewayClient.GetAsync("/auth/test/get-headers");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var headers = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
            {
                Assert.Null(headers![userClaim.HeaderKey]);
            }
            Assert.Null(headers![HeaderNames.Authorization]);
        }

        [Fact]
        public async Task ApplyTransform_WhenTokenIsExpired_ShouldAddNothingToHeadersAndRemoveTokenFromHeaders()
        {
            // Arrange
            var accountId = Guid.NewGuid().ToString();

            var downstreamServiceClient = _collectionCluster.DownstreamServiceFixture.CreateHttpClient();
            var tokenResponse = await downstreamServiceClient.GetAsync($"/test/generate-access-token?accountId={accountId}&expired=true");
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClientWithTls();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);

            // Act
            var response = await apiGatewayClient.GetAsync("/auth/test/get-headers");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var headers = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
            {
                Assert.Null(headers![userClaim.HeaderKey]);
            }
            Assert.Null(headers![HeaderNames.Authorization]);
        }

        [Fact]
        public async Task ApplyTransform_WhenTokenIsInvalid_ShouldAddNothingToHeadersAndRemoveTokenFromHeaders()
        {
            // Arrange
            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClientWithTls();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer Token");

            // Act
            var response = await apiGatewayClient.GetAsync("/auth/test/get-headers");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var headers = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
            {
                Assert.Null(headers![userClaim.HeaderKey]);
            }
            Assert.Null(headers![HeaderNames.Authorization]);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ApplyTransform_WhenTokenIsValid_ShouldAddClaimsToHeadersAndRemoveTokenFromHeaders(bool hasRoles)
        {
            // Arrange
            var accountId = Guid.NewGuid().ToString();
            var roles = new List<string>();
            var emailVerified = true;
            var preferredLanguage = "abc";

            var downstreamServiceClient = _collectionCluster.DownstreamServiceFixture.CreateHttpClient();

            string tokenUrl = $"/test/generate-access-token?accountId={accountId}&emailVerified={emailVerified}&preferredLang={preferredLanguage}";
            if (hasRoles)
            {
                roles.AddRange(["TestRole1", "TestRole2"]);
                tokenUrl += $"&roles={roles[0]}&roles={roles[1]}";
            }

            var tokenResponse = await downstreamServiceClient.GetAsync(tokenUrl);
            var token = await tokenResponse.Content.ReadAsStringAsync();

            var apiGatewayClient = _collectionCluster.ApiGatewayWebApp.CreateHttpClientWithTls();
            apiGatewayClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Bearer " + token);

            // Act
            var response = await apiGatewayClient.GetAsync("/auth/test/get-headers");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var headers = await response.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            Assert.Equal(accountId, headers![ApiGatewayAuthKeys.Claims.Id.HeaderKey]);
            if (hasRoles)
            {
                var rolesInHeaders = headers[ApiGatewayAuthKeys.Claims.Roles.HeaderKey].ToString().Split(',');
                foreach (var role in roles)
                {
                    Assert.Contains(role, rolesInHeaders);
                }
            }
            else
            {
                Assert.Null(headers[ApiGatewayAuthKeys.Claims.Roles.HeaderKey]);
            }
            Assert.Equal(emailVerified.ToString().ToLower(), headers[ApiGatewayAuthKeys.Claims.EmailVerified.HeaderKey]);
            Assert.Equal(preferredLanguage, headers[ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey]);
            Assert.NotNull(headers[ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey]);
            Assert.Null(headers[HeaderNames.Authorization]);
        }        
    }
}
