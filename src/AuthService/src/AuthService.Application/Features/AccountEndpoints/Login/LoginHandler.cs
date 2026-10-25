using System.Net;
using System.Security.Claims;
using AuthService.Application.Abstractions.Authentication;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Dtos.Crypto;
using AuthService.Application.Options;
using AuthService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Crypto;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response;
using Shared.Http.Response.Structures;

namespace AuthService.Application.Features.AccountEndpoints.Login
{
    public class LoginHandler
    {
        public static async Task<IResult> HandleAsync(
            LoginRequest request,
            IAuthDbContext authDbContext,
            IPasswordHasher passwordHasher,
            IOptions<AccountLockOptions> accountLockOptions,
            IJwtService jwtService,
            IHashVersions hashVersions,
            HttpContext context,
            ILogger<LoginHandler> logger)
        {
            // Check if the account exists
            var account = await authDbContext.Accounts
                .Where(a => a.Email == request.Email)
                .Include(a => a.Roles)
                .FirstOrDefaultAsync();

            if (account == null)
                return JsonResponseBuilder.Error(
                    HttpStatusCode.BadRequest,
                    [
                        new ErrorItem("auth_wrong_credentials", "The credentials are wrong.")
                    ]
                );

            // Check if the account is banned
            if (account.IsBanned)
                return JsonResponseBuilder.Error(
                    HttpStatusCode.Forbidden,
                    [
                        new ErrorItem("auth_account_banned", "The account is banned.")
                    ]
                );

            // Check if the account is locked
            if (account.IsLocked)
            {
                if (DateTimeOffset.UtcNow < account.UnlockDate)
                    return JsonResponseBuilder.Error(
                        HttpStatusCode.Locked,
                        [
                            new ErrorItem("auth_account_locked", "The account is locked.")
                        ],
                        metadata: new
                        {
                            UnlockDate = account.UnlockDate.Value.ToUnixTimeMilliseconds()
                        }
                    );
            }

            // Verify password
            var passwordVerificationResult = passwordHasher.Verify(account.PasswordHashed!, request.Password!, out var version);
            switch (passwordVerificationResult)
            {
                case EPasswordVerificationResult.Fail:
                    ++account.FailedLoginAttempts;
                    if (account.FailedLoginAttempts >= accountLockOptions.Value.NumberOfFailedAttempsBeforeLock)
                    {
                        var multiplier = Math.Min(account.FailedLoginAttempts - accountLockOptions.Value.NumberOfFailedAttempsBeforeLock + 1,
                            accountLockOptions.Value.MaxLockTimeMultiplier);

                        account.IsLocked = true;
                        account.UnlockDate = DateTimeOffset.UtcNow.AddMinutes(accountLockOptions.Value.LockTimeInMinutes * multiplier);

                        await authDbContext.SaveChangesAsync();

                        logger.LogWarning("The account is locked. User ID: {UserId}",
                            account.Id);

                        return JsonResponseBuilder.Error(
                            HttpStatusCode.Locked,
                            [
                                new ErrorItem("auth_account_locked_login", "The account is locked due to failed login attemps.")
                            ],
                            metadata: new
                            {
                                UnlockDate = account.UnlockDate.Value.ToUnixTimeMilliseconds()
                            }
                        );
                    }

                    await authDbContext.SaveChangesAsync();

                    return JsonResponseBuilder.Error(
                        HttpStatusCode.BadRequest,
                        [
                            new ErrorItem("auth_wrong_credentials", "The credentials are wrong.")
                        ],
                        metadata: new
                        {
                            account.FailedLoginAttempts
                        }
                    );
                case EPasswordVerificationResult.VersionNotFound:
                    logger.LogError("The version of the hashed password could not be found. Version: {Version}",
                        version);

                    return JsonResponseBuilder.Error(
                        HttpStatusCode.InternalServerError,
                        [
                            new ErrorItem("internal_server_error", "Something went wrong.")
                        ]
                    );
                case EPasswordVerificationResult.LengthMismatch:
                    logger.LogError("The expected length of the hashed password does not match.");

                    return JsonResponseBuilder.Error(
                        HttpStatusCode.InternalServerError,
                        [
                            new ErrorItem("internal_server_error", "Something went wrong.")
                        ]
                    );
                case EPasswordVerificationResult.Success:
                    if (account.IsLocked)
                    {
                        account.IsLocked = false;
                    }
                    if (account.FailedLoginAttempts != 0)
                    {
                        account.FailedLoginAttempts = 0;
                    }
                    break;
                case EPasswordVerificationResult.SuccessRehashNeeded:
                    if (account.IsLocked)
                    {
                        account.IsLocked = false;
                    }
                    if (account.FailedLoginAttempts != 0)
                    {
                        account.FailedLoginAttempts = 0;
                    }
                    
                    account.PasswordHashed = passwordHasher.Hash(request.Password!);
                    break;
                default:
                    logger.LogError("The result of the password verification is out of the range. Result: {Result}",
                        (int)passwordVerificationResult);

                    return JsonResponseBuilder.Error(
                        HttpStatusCode.InternalServerError,
                        [
                            new ErrorItem("internal_server_error", "Something went wrong.")
                        ]
                    );
            }

            // Generate JWT
            var claims = new List<Claim>()
            {
                new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, account.Id.ToString()),
                new Claim(ApiGatewayAuthKeys.Claims.EmailVerified.ClaimType, account.IsEmailVerified.ToString().ToLower()),
                new Claim(ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType, account.PreferredLanguage!.ToString()),
                new Claim(ApiGatewayAuthKeys.Claims.IssuedAt.ClaimType, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString())
            };

            var roles = account.Roles.Select(r => r.Name).ToList();
            foreach (var role in roles)
            {
                claims.Add(new Claim(ApiGatewayAuthKeys.Claims.Roles.ClaimType, role));
            }

            var jwt = jwtService.GenerateTokens(claims);

            // Add a new session to the DB
            var newSession = new Session(
                account.Id,
                ValueHasher.Hash(jwt.RefreshToken, hashVersions.CurrentVersion, hashVersions.GetHashOptions),
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
                    roles,
                },
                metadata: new
                {
                    account.IsEmailVerified,
                    account.PreferredLanguage
                }
            );
        }
    }
}
