using System.Net;
using System.Net.Http.Json;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Features.AccountEndpoints.CloseSessions;
using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Microsoft.EntityFrameworkCore;
using Common.Constants;
using Common.Http.Authentication.Constants;
using Common.Redis.Helpers.Extensions;
using Common.Test.Generators;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.CloseSessions
{
    [Collection(nameof(AuthApiCollection))]
    public class CloseSessionsHandlerTest
    {
        private const string _endpoint = "/accounts/sessions/close";

        private readonly AuthApiCollectionCluster _collectionCluster;

        public CloseSessionsHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task HandleAsync_WhenPasswordIsWrong_ShouldReturnForbidden()
        {
            // Arrange
            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();
            var account = new Account(
                EmailGenerator.Generate(),
                passwordHasher.Hash(PasswordGenerator.Generate()),
                SupportedLanguages.DefaultLanguage);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new CloseSessionsRequest(PasswordGenerator.GenerateValid());

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, account.Id.ToString() },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenPasswordIsCorrectAndThereIsNoSession_ShouldDoNothingAndAddTokensToBlacklistAndReturnNoContent()
        {
            // Arrange
            var password = PasswordGenerator.GenerateValid();
            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var account = new Account(
                EmailGenerator.Generate(),
                passwordHasher.Hash(password),
                SupportedLanguages.DefaultLanguage);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new CloseSessionsRequest(password);

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, account.Id.ToString() },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            var unixTimeInSeconds = await _collectionCluster.TokenBlacklistRedisFixture.GetDatabase()
                .LoadFromStringAsync<long>(account.Id.ToString());
            Assert.True(unixTimeInSeconds.isKeyFound, "The account ID as the key is not found in Redis");

            var time = DateTimeOffset.FromUnixTimeSeconds(unixTimeInSeconds.value); // Should not throw exception
        }

        [Fact]
        public async Task HandleAsync_WhenPasswordIsCorrectAndThereAreSessions_ShouldDeleteSessionsAndAddTokensToBlacklistAndReturnNoContent()
        {
            // Arrange
            var password = PasswordGenerator.GenerateValid();
            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var account = new Account(
                EmailGenerator.Generate(),
                passwordHasher.Hash(password),
                SupportedLanguages.DefaultLanguage);
            
            for (int i = 0; i < 3; ++i)
            {
                account.Sessions.Add(new Session(
                    account.Id,
                    refreshTokenHashed: StringGenerator.GenerateAlphanumeric(),
                    expirationDate: DateTimeOffset.UtcNow));
            }

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new CloseSessionsRequest(password);

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, account.Id.ToString() },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            authDbContext.ChangeTracker.Clear();

            var sessionFromDb = await authDbContext.Sessions
                .Where(s => s.AccountId == account.Id)
                .ToListAsync();
            Assert.Empty(sessionFromDb);

            var unixTimeInSeconds = await _collectionCluster.TokenBlacklistRedisFixture.GetDatabase()
                .LoadFromStringAsync<long>(account.Id.ToString());
            Assert.True(unixTimeInSeconds.isKeyFound, "The account ID as the key is not found in Redis");

            var time = DateTimeOffset.FromUnixTimeSeconds(unixTimeInSeconds.value); // Should not throw exception
        }
    }
}
