using System.Security.Claims;
using AuthService.Domain.Entities;
using AuthService.Persistence.DbContexts;
using Common.Constants;
using Common.Http.Authentication.Constants;
using Common.Http.Helpers.Authentication;
using Common.Test.Generators;
using Microsoft.AspNetCore.Http;
using Shared.Grpc.Auth.Admin;

namespace AuthService.Test.Integration.Infrastructure.Grpc
{
    public partial class AuthGrpcAdminServiceTest
    {
        [Fact]
        public async Task GetAccounts_WhenIsCalled_ShouldReturnListOfAccounts()
        {
            // Arrange
            var account = new Account(
                EmailGenerator.Generate(),
                PasswordGenerator.Generate(),
                SupportedLanguages.DefaultLanguage);

            var authDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<AuthDbContext>();
            await authDbContext.Accounts.AddAsync(account);
            await authDbContext.SaveChangesAsync();

            var claims = new List<Claim>()
            {
                new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, Guid.NewGuid().ToString()),
                new Claim(ApiGatewayAuthKeys.Claims.Roles.ClaimType, Policies.AdminPolicy.RoleName)
            };
            var httpContext = new DefaultHttpContext()
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, ApiGatewayAuthKeys.AuthenticationScheme))
            };

            var httpContextAccessor = _collectionCluster.AuthApiWebApp.GetService<IHttpContextAccessor>();
            httpContextAccessor.HttpContext = httpContext;

            await using var scope = _collectionCluster.AuthApiWebApp.GetScopedService<AuthGrpcAdmin.AuthGrpcAdminClient>(out var client);

            // Act
            var response = await client.GetAccountsAsync(new GetAccountsRequest()
            {
                Page = 1
            });

            // Assert
            Assert.NotNull(response);
            Assert.NotEmpty(response.Accounts);
        }
    }
}
