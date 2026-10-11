using System.Net;
using System.Runtime.InteropServices;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Crypto;
using Shared.Http.Authentication.Constants;
using Shared.Test.Generators;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.Logout
{
    [Collection(nameof(AuthApiCollection))]
    public class LogoutHandlerTest
    {
        private const string _endpoint = "/accounts/logout";

        private readonly AuthApiCollectionCluster _collectionCluster;

        public LogoutHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task Handle_WhenNotAuthenticated_ShouldReturnUnauthorized()
        {
            // Arrange
            var client = _collectionCluster.AuthApiWebApp.CreateHttpsClient();

            // Act
            var response = await client.DeleteAsync(_endpoint);

            // Assert
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Handle_WhenAuthenticatedAndThereIsNoCookies_ShouldNotDeleteSessionAndReturnNoContent()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(EmailGenerator.Generate(), PasswordGenerator.Generate(), SupportedLanguages.DefaultLanguage);
            account.Sessions.Add(new Session(account.Id, StringGenerator.GenerateAlphanumeric(), DateTimeOffset.UtcNow));
            
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpsClientWithCredentials(account.Id.ToString());

            // Act
            var response = await client.DeleteAsync(_endpoint);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            authDbContext.ChangeTracker.Clear();

            var sessionsFromDb = await authDbContext.Sessions
                .Where(s => s.AccountId == account.Id)
                .ToListAsync();
            Assert.Single(sessionsFromDb);
        }

        [Fact]
        public async Task Handle_WhenAuthenticatedAndCookieValuesAreAltered_ShouldNotDeleteSessionAndReturnNoContent()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(EmailGenerator.Generate(), PasswordGenerator.Generate(), SupportedLanguages.DefaultLanguage);
            account.Sessions.Add(new Session(account.Id, StringGenerator.GenerateAlphanumeric(), DateTimeOffset.UtcNow));

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpsClientWithCredentials(
                account.Id.ToString(),
                SupportedLanguages.DefaultLanguage,
                new Dictionary<string, string>()
                {
                    { CookieKeys.SessionId, Guid.NewGuid().ToString() },
                    { CookieKeys.RefreshToken, StringGenerator.GenerateAlphanumeric() }
                });
            
            // Act
            var response = await client.DeleteAsync(_endpoint);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            authDbContext.ChangeTracker.Clear();

            var sessionsFromDb = await authDbContext.Sessions
                .Where(s => s.AccountId == account.Id)
                .ToListAsync();
            Assert.Single(sessionsFromDb);
        }

        [Theory]
        [InlineData(true, false)]
        [InlineData(false, true)]
        [InlineData(false, false)]
        public async Task Handle_WhenAuthenticatedAndCookieValuesDoNotMatch_ShouldNotDeleteSessionAndReturnNoContent(bool matchSessionId, bool matchRefreshToken)
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            var hashVersions = _collectionCluster.AuthApiWebApp.GetService<IHashVersions>();

            var refreshToken = StringGenerator.GenerateAlphanumeric();
            var account = new Account(EmailGenerator.Generate(), PasswordGenerator.Generate(), SupportedLanguages.DefaultLanguage);
            account.Sessions.Add(new Session(account.Id, StringGenerator.GenerateAlphanumeric(), DateTimeOffset.UtcNow));

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpsClientWithCredentials(
                account.Id.ToString(),
                SupportedLanguages.DefaultLanguage,
                new Dictionary<string, string>()
                {
                    { CookieKeys.SessionId, matchSessionId ? account.Sessions.ElementAt(0).Id.ToString() : Guid.NewGuid().ToString() },
                    { CookieKeys.RefreshToken, matchRefreshToken ? refreshToken : StringGenerator.GenerateAlphanumeric() }
                });
            
            // Act
            var response = await client.DeleteAsync(_endpoint);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            authDbContext.ChangeTracker.Clear();

            var sessionsFromDb = await authDbContext.Sessions
                .Where(s => s.AccountId == account.Id)
                .ToListAsync();
            Assert.Single(sessionsFromDb);
        }

        [Fact]
        public async Task Handle_WhenAuthenticatedAndCookieValuesMatch_ShouldDeleteSessionAndReturnNoContent()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            var hashVersions = _collectionCluster.AuthApiWebApp.GetService<IHashVersions>();

            var refreshToken = StringGenerator.GenerateAlphanumeric();
            var account = new Account(EmailGenerator.Generate(), PasswordGenerator.Generate(), SupportedLanguages.DefaultLanguage);
            account.Sessions.Add(new Session(account.Id, ValueHasher.Hash(refreshToken, hashVersions.CurrentVersion, hashVersions.GetHashOptions), DateTimeOffset.UtcNow));

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpsClientWithCredentials(
                account.Id.ToString(),
                SupportedLanguages.DefaultLanguage,
                new Dictionary<string, string>()
                {
                    { CookieKeys.SessionId, account.Sessions.ElementAt(0).Id.ToString() },
                    { CookieKeys.RefreshToken, refreshToken }
                });

            // Act
            var response = await client.DeleteAsync(_endpoint);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            authDbContext.ChangeTracker.Clear();

            var sessionsFromDb = await authDbContext.Sessions
                .Where(s => s.AccountId == account.Id)
                .ToListAsync();
            Assert.Empty(sessionsFromDb);
        }
    }
}
