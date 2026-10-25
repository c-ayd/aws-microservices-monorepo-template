using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Features.AccountEndpoints.Login;
using AuthService.Application.Options;
using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using AuthService.Test.Integration.Application.Collections;
using Microsoft.EntityFrameworkCore;
using Shared.Constants;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response;
using Shared.Test.Generators;

namespace AuthService.Test.Integration.Application.Features.AccountEndpoints.Login
{
    [Collection(nameof(AuthApiCollection))]
    public class LoginHandlerTest
    {
        private const string _endpoint = "/accounts/login";

        private readonly AuthApiCollectionCluster _collectionCluster;

        public LoginHandlerTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task HandleAsync_WhenAccountDoesNotExist_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new LoginRequest(
                EmailGenerator.Generate(),
                StringGenerator.GenerateAlphanumeric());

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient("HttpsOnly");

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenAccountIsBanned_ShouldReturnForbidden()
        {
            // Arrange
            var email = EmailGenerator.Generate();
            var account = new Account(email, PasswordGenerator.Generate(), SupportedLanguages.DefaultLanguage);
            account.IsBanned = true;

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new LoginRequest(email, PasswordGenerator.GenerateValid());

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient("HttpsOnly");

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task HandleAsync_WhenAccountIsLocked_ShouldReturnLocked()
        {
            // Arrange
            var email = EmailGenerator.Generate();
            var password = PasswordGenerator.GenerateValid();

            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var account = new Account(email, passwordHasher.Hash(password), SupportedLanguages.DefaultLanguage);
            account.IsLocked = true;
            account.FailedLoginAttempts = 3;
            account.UnlockDate = DateTimeOffset.UtcNow.AddDays(1);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();
            
            var request = new LoginRequest(email, password);

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient("HttpsOnly");

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.Locked, response.StatusCode);
            
            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts.FindAsync(account.Id);
            Assert.Equal(3, accountFromDb!.FailedLoginAttempts);
        }

        [Fact]
        public async Task HandleAsync_WhenPasswordIsWrong_ShouldReturnBadRequest()
        {
            // Arrange
            var email = EmailGenerator.Generate();
            var password = PasswordGenerator.GenerateValid();

            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var account = new Account(email, passwordHasher.Hash(password), SupportedLanguages.DefaultLanguage);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new LoginRequest(email, password + "a");

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient("HttpsOnly");

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts.FindAsync(account.Id);
            Assert.Equal(1, accountFromDb!.FailedLoginAttempts);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        public async Task HandleAsync_WhenPasswordIsWrongAndAccountIsLocked_ShouldReturnLocked(int additionalFailedAttempts)
        {
            // Arrange
            var email = EmailGenerator.Generate();
            var password = PasswordGenerator.GenerateValid();

            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var accountLockOptions = _collectionCluster.AuthApiWebApp.GetOptions<AccountLockOptions>();
            var failedAttempts = accountLockOptions.NumberOfFailedAttempsBeforeLock - 1 + additionalFailedAttempts;

            var account = new Account(email, passwordHasher.Hash(password), SupportedLanguages.DefaultLanguage);
            account.FailedLoginAttempts = failedAttempts;

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new LoginRequest(email, password + "a");

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient("HttpsOnly");

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.Locked, response.StatusCode);

            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts.FindAsync(account.Id);
            Assert.Equal(failedAttempts + 1, accountFromDb!.FailedLoginAttempts);
            Assert.True(accountFromDb.IsLocked, "The account is not locked.");

            var now = DateTimeOffset.UtcNow;
            var multiplier = Math.Min(accountFromDb.FailedLoginAttempts - accountLockOptions.NumberOfFailedAttempsBeforeLock + 1, accountLockOptions.MaxLockTimeMultiplier);
            var lockTimeInMinutes = accountLockOptions.LockTimeInMinutes * multiplier;
            Assert.InRange((accountFromDb.UnlockDate!.Value - now).TotalMinutes, lockTimeInMinutes - 1, lockTimeInMinutes + 1);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task HandleAsync_WhenCredentialsAreCorrect_ShouldCreateSessionAndSetCookiesAndReturnOk(bool hasRoles)
        {
            // Arrange
            var email = EmailGenerator.Generate();
            var password = PasswordGenerator.GenerateValid();

            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var account = new Account(email, passwordHasher.Hash(password), SupportedLanguages.DefaultLanguage);
            account.IsEmailVerified = true;

            string? roleName = null;
            if (hasRoles)
            {
                roleName = StringGenerator.GenerateAlphanumeric();
                account.Roles.Add(new Role(roleName));
            }

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new LoginRequest(email, password);

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient("HttpsOnly");

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);
            var dto = await response.Content.ReadFromJsonAsync<Dto>();

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            Assert.NotNull(dto);
            Assert.NotNull(dto.Data.AccessToken);
            
            if (hasRoles)
            {
                Assert.Equal(roleName, dto.Data.Roles[0]);
            }
            else
            {
                Assert.Empty(dto.Data.Roles);
            }
            
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
                .FirstOrDefaultAsync();

            Assert.Single(accountFromDb!.Sessions);
        }

        [Fact]
        public async Task HandleAsync_WhenAccountIsLockedButLockedDateIsInPast_ShouldReturnOk()
        {
            // Arrange
            var email = EmailGenerator.Generate();
            var password = PasswordGenerator.GenerateValid();

            var passwordHasher = _collectionCluster.AuthApiWebApp.GetService<IPasswordHasher>();

            var account = new Account(email, passwordHasher.Hash(password), SupportedLanguages.DefaultLanguage);
            account.IsLocked = true;
            account.UnlockDate = DateTimeOffset.UtcNow.AddDays(-1);

            await using var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();

            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var request = new LoginRequest(email, password);

            var client = _collectionCluster.AuthApiWebApp.CreateHttpClient("HttpsOnly");

            // Act
            var response = await client.PostAsJsonAsync(_endpoint, request);

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            authDbContext.ChangeTracker.Clear();
            var accountFromDb = await authDbContext.Accounts.FindAsync(account.Id);
            Assert.Equal(0, accountFromDb!.FailedLoginAttempts);
            Assert.False(accountFromDb.IsLocked, "The account is locked.");
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
