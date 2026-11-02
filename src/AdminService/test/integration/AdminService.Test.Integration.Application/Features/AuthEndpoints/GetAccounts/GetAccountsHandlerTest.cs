using System.Net;
using System.Net.Http.Json;
using AdminService.Test.Integration.Application.Collections;
using Common.Constants;
using Common.Http.Authentication.Constants;
using Common.Http.Helpers.Authentication;
using Shared.Grpc.Auth.Admin;

namespace AdminService.Test.Integration.Application.Features.AuthEndpoints.GetAccounts
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
        public async Task HandleAsync_WhenIsCalled_ShouldReturnOkWithListOfAccounts()
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
            var response = await client.GetAsync("/auth/accounts?page=1");
            var dto = await response.Content.ReadFromJsonAsync<GetAccountsResponseDto>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.Equal(3, dto!.Data!.Accounts.Count);
        }

        private class GetAccountsResponseDto
        {
            public GetAccountsResponse? Data { get; set; }

            public class GetAccountsResponse
            {
                public List<AccountDto> Accounts { get; set; } = new List<AccountDto>();
            }
        }
    }
}
