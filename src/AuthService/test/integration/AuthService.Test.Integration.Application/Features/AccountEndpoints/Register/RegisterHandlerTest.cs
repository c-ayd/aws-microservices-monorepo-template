using System.Net;
using System.Net.Http.Json;
using AuthService.Application.Features.AccountEndpoints.Register;
using AuthService.Application.Options;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Microsoft.EntityFrameworkCore;
using Common.Constants;
using Common.Test.Generators;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.Register
{
    [Collection(nameof(AuthApiCollection))]
    public class RegisterHandlerTest
    {
        private const string _endpoint = "/accounts/register";

        private readonly AuthApiCollectionCluster _collectionCluster;

        public RegisterHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task HandleAsync_WhenAccountWithEmailExists_ShouldReturnConflict()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var email = EmailGenerator.Generate();
            authDbContext.Accounts.Add(new Account(
                email,
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage));
            await authDbContext.SaveChangesAsync();

            var request = new RegisterRequest(email, PasswordGenerator.GenerateValid());

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);
            
            // Assert
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenAccountDoesNotExist_ShouldCreateAccountAndTokenAndReturnNoContent()
        {
            // Arrange
            var tokenLifespan = TimeSpan.FromHours(_collectionCluster.AuthApiWebApp.GetOptions<TokenLifespansOptions>().EmailVerificationLifespanInHours)
                .TotalMinutes;

            var email = EmailGenerator.Generate();
            var request = new RegisterRequest(email, PasswordGenerator.GenerateValid());

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = authDbContext.Accounts
                .Where(a => a.Email == email)
                .Include(a => a.Tokens)
                .FirstOrDefault();
            Assert.NotNull(account);
            Assert.Single(account.Tokens);

            var token = account.Tokens.FirstOrDefault(t => t.Purpose == ETokenPurpose.EmailVerification);
            Assert.NotNull(token);

            var tokenLifespanInMinutes = (token.ExpirationDate - DateTimeOffset.UtcNow).TotalMinutes;
            Assert.InRange(tokenLifespanInMinutes, tokenLifespan - 1, tokenLifespan + 1);
        }
    }
}
