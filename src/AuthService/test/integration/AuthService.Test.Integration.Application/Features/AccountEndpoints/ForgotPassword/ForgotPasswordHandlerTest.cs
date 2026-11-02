using System.Net;
using System.Net.Http.Json;
using AuthService.Application.Features.AccountEndpoints.ForgotPassword;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Common.Constants;
using Common.Test.Generators;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.ForgotPassword
{
    [Collection(nameof(AuthApiCollection))]
    public class ForgotPasswordHandlerTest
    {
        private const string _endpoint = "/accounts/forgot-password";

        private readonly AuthApiCollectionCluster _collectionCluster;

        public ForgotPasswordHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task HandleAsync_WhenAccountDoesNotExist_ShouldReturnNotFound()
        {
            // Arrange
            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            var request = new ForgotPasswordRequest(EmailGenerator.Generate());

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenAccountExists_ShouldReturnOkAndAddTokenToDb()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            
            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            var request = new ForgotPasswordRequest(account.Email);

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            authDbContext.ChangeTracker.Clear();
            var tokensFromDb = await authDbContext.Tokens
                .Where(t => t.AccountId == account.Id)
                .ToListAsync();

            Assert.Single(tokensFromDb);
            Assert.Equal(ETokenPurpose.ResetPassword, tokensFromDb[0].Purpose);
        }
    }
}
