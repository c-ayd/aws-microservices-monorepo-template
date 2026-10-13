using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Http.Authentication;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response;
using Shared.Test.Generators;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.GetSessions
{
    [Collection(nameof(AuthApiCollection))]
    public class GetSessionsHandlerTest
    {
        private const string _endpoint = "/accounts/sessions";
        
        private readonly AuthApiCollectionCluster _collectionCluster;

        public GetSessionsHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task Handle_WhenNotAuthenticated_ShouldReturnUnauthorized()
        {
            // Arrange
            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient("HttpsOnly");

            // Act
            var response = await client.GetAsync(_endpoint);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Handle_WhenAuthenticatedAndThereAreSessions_ShouldReturnOkWithNotExpiredSessions()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var numberOfSessions = 3;
            var account = new Account(EmailGenerator.Generate(), PasswordGenerator.Generate(), SupportedLanguages.DefaultLanguage);
            for (int i = 0; i < numberOfSessions; ++i)
            {
                account.Sessions.Add(new Session(account.Id, StringGenerator.GenerateAlphanumeric(), DateTimeOffset.UtcNow.AddDays(1)));
            }
            account.Sessions.Add(new Session(account.Id, StringGenerator.GenerateAlphanumeric(), DateTimeOffset.UtcNow.AddDays(-1)));

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();
            
            var client = _collectionCluster.AuthApiWebApp.CreateHttpClientWithHeaders("HttpsOnly",
                new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, account.Id.ToString() },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.GetAsync(_endpoint);
            var dto = await response.Content.ReadFromJsonAsync<Dto>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.NotNull(dto);
            Assert.Equal(numberOfSessions, dto.Data.Count);
        }

        private class Dto
        {
            public List<SessionDto> Data { get; set; } = new List<SessionDto>();

            public class SessionDto
            {
                public DateTimeOffset CreatedDate { get; set; }
                public DateTimeOffset? UpdatedDate { get; set; }
                public IPAddress? IpAddress { get; set; }
                public string? DeviceInfo { get; set; }
            }
        }
    }
}
