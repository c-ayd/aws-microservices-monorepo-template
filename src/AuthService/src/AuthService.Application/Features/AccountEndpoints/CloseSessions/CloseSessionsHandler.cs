using System.Net;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Abstractions.DistributedCaches;
using AuthService.Application.Dtos.Crypto;
using AuthService.Application.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shared.Http.Response;
using Shared.Http.Response.Structures;

namespace AuthService.Application.Features.AccountEndpoints.CloseSessions
{
    public class CloseSessionsHandler
    {
        public static async Task<IResult> HandleAsync(
            CloseSessionsRequest request,
            HttpContext context,
            IAuthDbContext authDbContext,
            IPasswordHasher passwordHasher,
            ITokenBlacklistRedis tokenBlacklist,
            IOptions<JwtOptions> jwtOptions,
            ILogger<CloseSessionsHandler> logger)
        {
            // First check the password
            var accountId = Guid.Parse(context.User.Identity!.Name!);
            var passwordHashed = await authDbContext.Accounts
                .Where(a => a.Id == accountId)
                .Select(a => a.PasswordHashed)
                .FirstOrDefaultAsync();

            var passwordVerificationResult = passwordHasher.Verify(passwordHashed!, request.Password!, out var version);
            switch (passwordVerificationResult)
            {
                case EPasswordVerificationResult.Fail:
                    return JsonResponseBuilder.Error(
                        HttpStatusCode.Forbidden,
                        [
                            new ErrorItem("auth_password_wrong", "The password is wrong.")
                        ]
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
                case EPasswordVerificationResult.SuccessRehashNeeded:
                    // Add all access tokens that are generated in the past to the blacklist
                    await tokenBlacklist.AddAsync(context.User.Identity!.Name!, TimeSpan.FromMinutes(jwtOptions.Value.AccessTokenLifespanInMinutes));

                    // Delete the sessions
                    await authDbContext.Sessions
                        .Where(s => s.AccountId == accountId)
                        .ExecuteDeleteAsync();

                    return JsonResponseBuilder.Success(HttpStatusCode.NoContent);
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
        }
    }
}
