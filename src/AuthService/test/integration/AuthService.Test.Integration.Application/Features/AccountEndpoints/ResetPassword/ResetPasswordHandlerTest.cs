using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Dtos.Crypto;
using AuthService.Application.Features.AccountEndpoints.ResetPassword;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Common.Constants;
using Common.Crypto;
using Common.Crypto.Structures;
using Common.Test.Generators;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.ResetPassword
{
    [Collection(nameof(AuthApiCollection))]
    public class ResetPasswordHandlerTest
    {
        private const string _endpoint = "/accounts/reset-password";

        private readonly AuthApiCollectionCluster _collectionCluster;

        public ResetPasswordHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task HandleAsync_WhenTokenIsNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            var request = new ResetPasswordRequest(PasswordGenerator.GenerateValid(), null);

            // Act
            var response = await client.PostAsJsonAsync($"{_endpoint}?token={TokenGenerator.GenerateBase64UrlSafe()}.{TokenGenerator.GenerateBase64UrlSafe()}", request);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenTokenPurposeIsNotResetPassword_ShouldReturnNotFound()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);

            var tokenValue = TokenGenerator.GenerateBase64UrlSafe();
            var token = new Token(
                account.Id,
                ETokenPurpose.EmailVerification,
                valueHashed: Convert.ToBase64String(ValueHasher.Hash(
                    value: Encoding.UTF8.GetBytes(tokenValue),
                    version: 1,
                    (version) => new HashOptions(SHA256.Create, SaltSize: 32),
                    out var salt)),
                expirationDate: DateTimeOffset.UtcNow.AddDays(1));

            account.Tokens.Add(token);
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            var request = new ResetPasswordRequest(PasswordGenerator.GenerateValid(), null);

            // Act
            var response = await client.PostAsJsonAsync($"{_endpoint}?token={tokenValue}.{WebEncoders.Base64UrlEncode(salt)}", request);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenTokenIsExpired_ShouldReturnNotFoundAndDeleteToken()
        {
            // Arrange
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);

            var tokenValue = TokenGenerator.GenerateBase64UrlSafe();
            var token = new Token(
                account.Id,
                ETokenPurpose.ResetPassword,
                valueHashed: Convert.ToBase64String(ValueHasher.Hash(
                    value: Encoding.UTF8.GetBytes(tokenValue),
                    version: 1,
                    (version) => new HashOptions(SHA256.Create, SaltSize: 32),
                    out var salt)),
                expirationDate: DateTimeOffset.UtcNow.AddDays(-1));

            account.Tokens.Add(token);
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            var request = new ResetPasswordRequest(PasswordGenerator.GenerateValid(), null);

            // Act
            var response = await client.PostAsJsonAsync($"{_endpoint}?token={tokenValue}.{WebEncoders.Base64UrlEncode(salt)}", request);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            authDbContext.ChangeTracker.Clear();
            var tokenFromDb = await authDbContext.Tokens
                .Where(t => t.Id == token.Id)
                .FirstOrDefaultAsync();

            Assert.Null(tokenFromDb);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task HandleAsync_WhenTokenIsValid_ShouldReturnOkAndResetPasswordAndDeleteToken(bool closeSessions)
        {
            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);

            var tokenValue = TokenGenerator.GenerateBase64UrlSafe();
            var token = new Token(
                account.Id,
                ETokenPurpose.ResetPassword,
                valueHashed: Convert.ToBase64String(ValueHasher.Hash(
                    value: Encoding.UTF8.GetBytes(tokenValue),
                    version: 1,
                    (version) => new HashOptions(SHA256.Create, SaltSize: 32),
                    out var salt)),
                expirationDate: DateTimeOffset.UtcNow.AddDays(1));

            int numberOfOpenSessions = 3;
            for (int i = 0; i < numberOfOpenSessions; ++i)
            {
                account.Sessions.Add(new Session(
                    account.Id,
                    refreshTokenHashed: StringGenerator.GenerateAlphanumeric(),
                    expirationDate: DateTimeOffset.UtcNow.AddDays(1)));
            }

            account.Tokens.Add(token);
            
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            var newPassword = PasswordGenerator.GenerateValid();
            var request = new ResetPasswordRequest(newPassword, closeSessions);

            // Act
            var response = await client.PostAsJsonAsync($"{_endpoint}?token={tokenValue}.{WebEncoders.Base64UrlEncode(salt)}", request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts
                .Where(a => a.Id == account.Id)
                .Include(a => a.Tokens)
                .Include(a => a.Sessions)
                .FirstOrDefaultAsync();

            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();
            
            Assert.Equal(EPasswordVerificationResult.Success, passwordHasher.Verify(accountFromDb!.PasswordHashed!, newPassword, out _));
            Assert.Empty(accountFromDb.Tokens);
            Assert.Equal(closeSessions ? 0 : numberOfOpenSessions, accountFromDb.Sessions.Count);
        }
    }
}
