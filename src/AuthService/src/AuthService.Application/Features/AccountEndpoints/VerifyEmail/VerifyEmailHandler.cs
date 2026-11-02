using System.Net;
using System.Security.Claims;
using System.Text;
using AuthService.Application.Abstractions.Authentication;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Errors;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using Common.Constants.Errors;
using Common.Crypto;
using Common.Http.Authentication.Constants;
using Common.Http.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AuthService.Application.Features.AccountEndpoints.VerifyEmail
{
    public class VerifyEmailHandler
    {
        public static async Task<IResult> HandleAsync(
            [FromQuery] string token,
            IHashVersions hashVersions,
            IAuthDbContext authDbContext,
            IJwtService jwtService,
            HttpContext context,
            ILogger<VerifyEmailHandler> logger)
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
            var accountId = emailVerificationToken.AccountId;
            await using (var transaction = await authDbContext.Database.BeginTransactionAsync())
            {
                try
                {
                    await authDbContext.Accounts
                        .Where(a => a.Id == accountId)
                        .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.IsEmailVerified, true));

                    authDbContext.Tokens.Remove(emailVerificationToken);
                    await authDbContext.SaveChangesAsync();

                    await transaction.CommitAsync();
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, "Something went wrong while verifying email address. Account ID: {AccountId} Message: {Message}",
                        accountId,
                        exception.Message);

                    await transaction.RollbackAsync();

                    throw;
                }
            }

            // Create JWT token to log in immediately if not logged in already
            if (context.User.Identity != null && context.User.Identity.IsAuthenticated)
                return JsonResponseBuilder.Success(HttpStatusCode.OK);

            var account = await authDbContext.Accounts
                .Where(a => a.Id == accountId)
                .Select(a => new
                {
                    a.PreferredLanguage,
                    Roles = a.Roles.Select(r => r.Name).ToList()
                })
                .FirstOrDefaultAsync();

            if (account == null) // This should never happen
                return JsonResponseBuilder.Error(
                    HttpStatusCode.InternalServerError,
                    errors: [
                        CommonErrors.UnknownError
                    ]
                );

            var claims = new List<Claim>()
            {
                new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, accountId.ToString()),
                new Claim(ApiGatewayAuthKeys.Claims.EmailVerified.ClaimType, true.ToString().ToLower()),
                new Claim(ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType, account.PreferredLanguage!.ToString()),
                new Claim(ApiGatewayAuthKeys.Claims.IssuedAt.ClaimType, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            foreach (var role in account.Roles)
            {
                claims.Add(new Claim(ApiGatewayAuthKeys.Claims.Roles.ClaimType, role));
            }

            var jwt = jwtService.GenerateTokens(claims);

            // Add a new session to the DB
            var newSession = new Session(
                accountId,
                refreshTokenHashed: Convert.ToBase64String(ValueHasher.Hash(
                    value: Encoding.UTF8.GetBytes(jwt.RefreshToken).AsSpan(),
                    hashVersions.CurrentVersion,
                    hashVersions.GetHashOptions)),
                jwt.RefreshTokenExpirationDate,
                context.Connection.RemoteIpAddress,
                context.Request.Headers.UserAgent);

            await authDbContext.Sessions.AddAsync(newSession);
            await authDbContext.SaveChangesAsync();

            // Add the session ID and refresh token to the cookies
            context.Response.Cookies.Append(CookieKeys.SessionId, newSession.Id.ToString(), new CookieOptions()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = jwt.RefreshTokenExpirationDate
            });
            context.Response.Cookies.Append(CookieKeys.RefreshToken, jwt.RefreshToken, new CookieOptions()
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = jwt.RefreshTokenExpirationDate
            });

            return JsonResponseBuilder.SuccessWithData(
                HttpStatusCode.OK,
                data: new
                {
                    jwt.AccessToken,
                    account.Roles,
                },
                metadata: new
                {
                    IsEmailVerified = true,
                    account.PreferredLanguage
                }
            );
        }
    }
}
