using System.Security.Claims;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using AuthService.Persistence.DbContexts;
using Common.Constants;
using Common.Http.Authentication.Constants;
using Common.Test.Generators;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Shared.Grpc.Auth.Admin;

namespace AuthService.Test.Integration.Infrastructure.Grpc
{
    public partial class AuthGrpcAdminServiceTest
    {
        [Fact]
        public async Task GetAccount_WhenAccountIsNotFound_ShouldReturnNotFound()
        {
            // Arrange
            var claims = new List<Claim>()
            {
                new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, Guid.NewGuid().ToString()),
                new Claim(ApiGatewayAuthKeys.Claims.Roles.ClaimType, "Admin")
            };
            var httpContext = new DefaultHttpContext()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiGatewayAuthKeys.AuthenticationScheme))
            };

            var httpContextAccessor = _collectionCluster.AuthApiWebApp.GetService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = httpContext;

            await using var scope = _collectionCluster.AuthApiWebApp.GetScopedService<AuthGrpcAdmin.AuthGrpcAdminClient>(out var client);

            // Act
            var exception = await Record.ExceptionAsync(async () =>
            {
                await client.GetAccountAsync(new GetAccountRequest()
                {
                    Id = Guid.NewGuid().ToString()
                });
            });

            // Assert
            Assert.NotNull(exception);
            Assert.IsType<RpcException>(exception);
            Assert.Equal(StatusCode.NotFound, ((RpcException)exception).StatusCode);
        }

        [Fact]
        public async Task GetAccount_WhenAccountIsFound_ShouldReturnAccountInfo()
        {
            // Arrange
            var role = new Role("Admin");

            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);
            account.Sessions.Add(new Session(
                account.Id,
                refreshTokenHashed: StringGenerator.GenerateAlphanumeric(),
                expirationDate: DateTimeOffset.UtcNow));
            account.Tokens.Add(new Token(
                account.Id,
                ETokenPurpose.EmailVerification,
                valueHashed: StringGenerator.GenerateAlphanumeric(),
                expirationDate: DateTime.UtcNow));

            account.Roles.Add(role);
            role.Accounts.Add(account);

            var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var claims = new List<Claim>()
            {
                new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, Guid.NewGuid().ToString()),
                new Claim(ApiGatewayAuthKeys.Claims.Roles.ClaimType, "Admin")
            };
            var httpContext = new DefaultHttpContext()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiGatewayAuthKeys.AuthenticationScheme))
            };

            var httpContextAccessor = _collectionCluster.AuthApiWebApp.GetService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = httpContext;

            await using var scope = _collectionCluster.AuthApiWebApp.GetScopedService<AuthGrpcAdmin.AuthGrpcAdminClient>(out var client);

            // Act
            var response = await client.GetAccountAsync(new GetAccountRequest()
            {
                Id = account.Id.ToString()
            });

            // Assert
            Assert.NotNull(response);

            Assert.Equal(account.Email, response.Account.Email);
            Assert.Equal(account.IsEmailVerified, response.Account.IsEmailVerified);
            Assert.Equal(account.IsBanned, response.Account.IsBanned);
            Assert.Equal(account.IsDeleted, response.Account.IsDeleted);
            Assert.Equal(account.IsLocked, response.Account.IsLocked);
            Assert.Equal(account.FailedLoginAttempts, response.Account.FailedLoginAttempts);
            Assert.Equal(account.CreatedDate.ToUnixTimeSeconds(), response.Account.CreatedDateInUnixTimeSeconds);
            Assert.False(response.Account.HasUpdatedDateInUnixTimeSeconds);
            Assert.False(response.Account.HasUnlockDateInUnixTimeSeconds);
            Assert.False(response.Account.HasDeletedDateInUnixTimeSeconds);

            Assert.Single(response.Roles);
            Assert.Equal(role.Name, response.Roles[0].Name);
            Assert.Equal(role.CreatedDate.ToUnixTimeSeconds(), response.Roles[0].CreatedDateInUnixTimeSeconds);

            Assert.Single(response.Sessions);
            Assert.Equal(account.Sessions[0].CreatedDate.ToUnixTimeSeconds(), response.Sessions[0].CreatedDateInUnixTimeSeconds);
            Assert.Equal(account.Sessions[0].ExpirationDate.ToUnixTimeSeconds(), response.Sessions[0].ExpirationDateInUnixTimeSeconds);
            Assert.False(response.Sessions[0].HasUpdatedDateInUnixTimeSeconds);
            Assert.False(response.Sessions[0].HasIpAddress);
            Assert.False(response.Sessions[0].HasDeviceInfo);

            Assert.Single(response.Tokens);
            Assert.Equal(account.Tokens[0].CreatedDate.ToUnixTimeSeconds(), response.Tokens[0].CreatedDateInUnixTimeSeconds);
            Assert.Equal(account.Tokens[0].ExpirationDate.ToUnixTimeSeconds(), response.Tokens[0].ExpirationDateInUnixTimeSeconds);
            Assert.Equal(account.Tokens[0].Purpose.ToString(), response.Tokens[0].Purpose);
        }
    }
}
