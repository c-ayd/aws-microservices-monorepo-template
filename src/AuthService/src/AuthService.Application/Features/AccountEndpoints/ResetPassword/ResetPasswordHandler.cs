using System.Net;
using System.Text;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Abstractions.DistributedCaches;
using AuthService.Application.Errors;
using AuthService.Application.Options;
using AuthService.Domain.Enums;
using Common.Crypto;
using Common.Http.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AuthService.Application.Features.AccountEndpoints.ResetPassword
{
    public class ResetPasswordHandler
    {
        public static async Task<IResult> HandleAsync(
            [FromQuery] string token,
            ResetPasswordRequest request,
            IHashVersions hashVersions,
            IAuthDbContext authDbContext,
            IPasswordHasher passwordHasher,
            ITokenBlacklistRedis tokenBlacklist,
            IOptions<JwtOptions> jwtOptions,
            ILogger<ResetPasswordHandler> logger)
        {
            // Both token value and salt value is in the same query parameter.
            // The first index is the token value, the second index is the salt value.
            var values = token.Split('.');
            var hashedToken = Convert.ToBase64String(ValueHasher.Hash(
                value: Encoding.UTF8.GetBytes(values[0]),
                salt: WebEncoders.Base64UrlDecode(values[1]),
                hashVersions.CurrentVersion,
                hashVersions.GetHashOptions));

            // Get reset password token
            var resetPasswordToken = await authDbContext.Tokens
                .Where(t => t.ValueHashed == hashedToken)
                .FirstOrDefaultAsync();

            if (resetPasswordToken == null ||
                resetPasswordToken.Purpose != ETokenPurpose.ResetPassword)
                return JsonResponseBuilder.Error(
                    HttpStatusCode.NotFound,
                    errors: [
                        ServiceErrors.TokenNotFound
                    ]
                );

            // If token is expired but still in the DB, delete it before returning response
            if (DateTimeOffset.UtcNow > resetPasswordToken.ExpirationDate)
            {
                authDbContext.Tokens.Remove(resetPasswordToken);
                await authDbContext.SaveChangesAsync();

                return JsonResponseBuilder.Error(
                    HttpStatusCode.NotFound,
                    errors: [
                        ServiceErrors.TokenNotFound
                    ]
                );
            }

            // Reset the password and remove the token
            var accountId = resetPasswordToken.AccountId;
            await using (var transaction = await authDbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    await authDbContext.Accounts
                        .Where(a => a.Id == accountId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.PasswordHashed, passwordHasher.Hash(request.Password!)));

                    authDbContext.Tokens.Remove(resetPasswordToken);
                    await authDbContext.SaveChangesAsync();

                    // Delete all active sessions if requested
                    if (request.CloseSessions.HasValue && request.CloseSessions.Value)
                    {
                        // Add all access tokens that are generated in the past to the blacklist
                        await tokenBlacklist.AddAsync(accountId.ToString(), TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenLifespanInMinutes));

                        await authDbContext.Sessions
                            .Where(s => s.AccountId == accountId)
                            .ExecuteDeleteAsync();
                    }
                    
                    await transaction.CommitAsync();
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Something went wrong while resetting password. Account ID: {AccountId} Message: {Message}",
                        accountId,
                        exception.Message);

                    await transaction.RollbackAsync();

                    throw;
                }
            }

            return JsonResponseBuilder.Success(HttpStatusCode.OK);
        }
    }
}
