using System.Net;
using System.Text;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Errors;
using AuthService.Domain.Enums;
using Common.Crypto;
using Common.Http.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace AuthService.Application.Features.AccountEndpoints.VerifyEmail
{
    public static class VerifyEmailHandler
    {
        public static async Task<IResult> HandleAsync(
            [FromQuery] string token,
            IHashVersions hashVersions,
            IAuthDbContext authDbContext)
        {
            // Both token value and salt value is in the same query parameter.
            // The first index is the token value, the second index is the salt value.
            var values = token.Split('.');
            var hashedToken = Convert.ToBase64String(ValueHasher.Hash(
                value: Encoding.UTF8.GetBytes(values[0]),
                salt: WebEncoders.Base64UrlDecode(values[1]),
                hashVersions.CurrentVersion,
                hashVersions.GetHashOptions));

            // Get email veritication token
            var emailVerificationToken = await authDbContext.Tokens
                .Where(t => t.ValueHashed == hashedToken)
                .FirstOrDefaultAsync();

            if (emailVerificationToken == null ||
                emailVerificationToken.Purpose != ETokenPurpose.EmailVerification)
                return JsonResponseBuilder.Error(
                    HttpStatusCode.NotFound,
                    errors: [
                        ServiceErrors.TokenNotFound
                    ]
                );

            // If token is expired but still in the DB, delete it before returning response
            if (DateTimeOffset.UtcNow > emailVerificationToken.ExpirationDate)
            {
                authDbContext.Tokens.Remove(emailVerificationToken);
                await authDbContext.SaveChangesAsync();

                return JsonResponseBuilder.Error(
                    HttpStatusCode.NotFound,
                    errors: [
                        ServiceErrors.TokenNotFound
                    ]
                );
            }

            // Set the email verification column to true and delete the token
            await authDbContext.Accounts
                .Where(a => a.Id == emailVerificationToken.AccountId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsEmailVerified, true));

            authDbContext.Tokens.Remove(emailVerificationToken);
            await authDbContext.SaveChangesAsync();

            return JsonResponseBuilder.Success(HttpStatusCode.OK);
        }
    }
}
