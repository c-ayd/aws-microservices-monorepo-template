using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Common.Constants;
using Common.Crypto;
using Common.Crypto.Structures;
using Common.Http.Authentication.Constants;
using Common.Http.Helpers.Authentication;
using Common.Test.Generators;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.VerifyEmail
{
    [Collection(nameof(AuthApiCollection))]
    public class VerifyEmailHandlerTest
    {
        private const string _endpoint = "/accounts/verify-email";

        private readonly AuthApiCollectionCluster _collectionCluster;

        public VerifyEmailHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task HandleAsync_WhenTokenIsNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            // Act
            var response = await client.PostAsync($"{_endpoint}?token={TokenGenerator.GenerateBase64UrlSafe()}.{TokenGenerator.GenerateBase64UrlSafe()}", null);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenTokenPurposeIsNotEmailVerification_ShouldReturnNotFound()
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
                expirationDate: DateTimeOffset.UtcNow.AddDays(1));

            account.Tokens.Add(token);
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient();

            // Act
            var response = await client.PostAsync($"{_endpoint}?token={tokenValue}.{WebEncoders.Base64UrlEncode(salt)}", null);

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
                ETokenPurpose.EmailVerification,
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

            // Act
            var response = await client.PostAsync($"{_endpoint}?token={tokenValue}.{WebEncoders.Base64UrlEncode(salt)}", null);

            // Assert
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

            authDbContext.ChangeTracker.Clear();
            var tokenFromDb = await authDbContext.Tokens
                .Where(t => t.Id == token.Id)
                .FirstOrDefaultAsync();

            Assert.Null(tokenFromDb);
        }

        [Fact]
        public async Task HandleAsync_WhenTokenIsValidAndAuthenticated_ShouldReturnOkAndSetEmailVerifiedColumnAndDeleteToken()
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

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient(
                headers: new Dictionary<string, string>()
                {
                    { ApiGatewayAuthKeys.Claims.Id.HeaderKey, Guid.NewGuid().ToString() },
                    { ApiGatewayAuthKeys.Claims.Roles.HeaderKey, Policies.AdminPolicy.RoleName },
                    { ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, SupportedLanguages.DefaultLanguage },
                    { ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString() }
                });

            // Act
            var response = await client.PostAsync($"{_endpoint}?token={tokenValue}.{WebEncoders.Base64UrlEncode(salt)}", null);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts
                .Where(a => a.Id == account.Id)
                .Include(a => a.Tokens)
                .FirstOrDefaultAsync();

            Assert.True(accountFromDb!.IsEmailVerified, "The email column is not set to true");
            Assert.Empty(accountFromDb.Tokens);
        }

        [Fact]
        public async Task HandleAsync_WhenTokenIsValidAndNotAuthenticated_ShouldReturnOkAndSetEmailVerifiedColumnAndDeleteToken()
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

            // Act
            var response = await client.PostAsync($"{_endpoint}?token={tokenValue}.{WebEncoders.Base64UrlEncode(salt)}", null);
            var dto = await response.Content.ReadFromJsonAsync<Dto>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.NotNull(dto);
            Assert.NotNull(dto.Data.AccessToken);
            Assert.Empty(dto.Data.Roles);
            Assert.True(dto.Metadata.IsEmailVerified);
            Assert.Equal(SupportedLanguages.DefaultLanguage, dto.Metadata.PreferredLanguage);

            response.Headers.TryGetValues("Set-Cookie", out var cookieValues);
            Assert.NotNull(cookieValues);
            Assert.True(cookieValues.Any(c => c.StartsWith(CookieKeys.SessionId)), "The cookie is not set for the session ID.");
            Assert.True(cookieValues.Any(c => c.StartsWith(CookieKeys.RefreshToken)), "The cookie is not set for the refresh token.");

            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts
                .Where(a => a.Id == account.Id)
                .Include(a => a.Sessions)
                .Include(a => a.Tokens)
                .FirstOrDefaultAsync();

            Assert.Single(accountFromDb!.Sessions);

            Assert.True(accountFromDb.IsEmailVerified, "The email column is not set to true");
            Assert.Empty(accountFromDb.Tokens);
        }

        private class Dto
        {
            public LoginDto Data { get; set; } = null!;
            public MetadataDto Metadata { get; set; } = null!;

            public class LoginDto
            {
                public string AccessToken { get; set; } = null!;
                public List<string> Roles { get; set; } = new List<string>();
            }

            public class MetadataDto
            {
                public bool IsEmailVerified { get; set; }
                public string PreferredLanguage { get; set; } = null!;
            }
        }
    }
}
