using System.Net;
using System.Net.Http.Json;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Features.AccountEndpoints.CloseSession;
using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Shared.Constants;
using Shared.Http.Authentication.Constants;
using Shared.Test.Generators;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.CloseSession
{
    [Collection(nameof(AuthApiCollection))]
    public class CloseSessionHandlerTest
    {
        private const string _endpoint = "/accounts/session/{0}/close";

        private readonly AuthApiCollectionCluster _collectionCluster;

        public CloseSessionHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task HandleAsync_WhenPasswordIsWrong_ShouldReturnForbidden()
        {
            // Arrange
            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();
            var account = new Account(EmailGenerator.Generate(), passwordHasher.Hash(PasswordGenerator.Generate()), SupportedLanguages.DefaultLanguage);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new CloseSessionRequest(PasswordGenerator.GenerateValid());

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClientWithHeaders("HttpsOnly",
                new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, account.Id.ToString() },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.PostAsJsonAsync(string.Format(_endpoint, Guid.NewGuid().ToString()), request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenPasswordIsCorrectAndSessionDoesNotExist_ShouldDoNothingAndReturnNoContent()
        {
            // Arrange
            var password = PasswordGenerator.GenerateValid();
            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var account = new Account(EmailGenerator.Generate(), passwordHasher.Hash(password), SupportedLanguages.DefaultLanguage);
            var session = new Session(account.Id, StringGenerator.GenerateAlphanumeric(), DateTimeOffset.UtcNow);
            account.Sessions.Add(session);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new CloseSessionRequest(password);

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClientWithHeaders("HttpsOnly",
                new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, account.Id.ToString() },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.PostAsJsonAsync(string.Format(_endpoint, Guid.NewGuid().ToString()), request);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            authDbContext.ChangeTracker.Clear();

            var sessionFromDb = await authDbContext.Sessions.FindAsync(session.Id);
            Assert.NotNull(sessionFromDb);
        }

        [Fact]
        public async Task HandleAsync_WhenPasswordIsCorrectAndSessionExists_ShouldDeleteSessionAndReturnNoContent()
        {
            // Arrange
            var password = PasswordGenerator.GenerateValid();
            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var account = new Account(EmailGenerator.Generate(), passwordHasher.Hash(password), SupportedLanguages.DefaultLanguage);
            var session = new Session(account.Id, StringGenerator.GenerateAlphanumeric(), DateTimeOffset.UtcNow);
            account.Sessions.Add(session);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new CloseSessionRequest(password);

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClientWithHeaders("HttpsOnly",
                new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, account.Id.ToString() },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.PostAsJsonAsync(string.Format(_endpoint, session.Id.ToString()), request);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            authDbContext.ChangeTracker.Clear();

            var sessionFromDb = await authDbContext.Sessions.FindAsync(session.Id);
            Assert.Null(sessionFromDb);
        }
    }
}
