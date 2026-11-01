using System.Net;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Dtos.Crypto;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Common.Http.Response;
using AuthService.Application.Errors;
using Common.Constants.Errors;

namespace AuthService.Application.Features.AccountEndpoints.CloseSession
{
    public class CloseSessionHandler
    {
        public static async Task<IResult> HandleAsync(
            [FromRoute] Guid sessionId,
            CloseSessionRequest request,
            HttpContext context,
            IAuthDbContext authDbContext,
            IPasswordHasher passwordHasher,
            ILogger<CloseSessionHandler> logger)
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
                        errors: [
                            ServiceErrors.WrongPasswordError
                        ]
                    );
                case EPasswordVerificationResult.VersionNotFound:
                    logger.LogError("The version of the hashed password could not be found. Version: {Version}",
                        version);

                    return JsonResponseBuilder.Error(
                        HttpStatusCode.InternalServerError,
                        errors: [
                            CommonErrors.UnknownError
                        ]
                    );
                case EPasswordVerificationResult.LengthMismatch:
                    logger.LogError("The expected length of the hashed password does not match.");

                    return JsonResponseBuilder.Error(
                        HttpStatusCode.InternalServerError,
                        errors: [
                            CommonErrors.UnknownError
                        ]
                    );
                case EPasswordVerificationResult.Success:
                case EPasswordVerificationResult.SuccessRehashNeeded:
                    // Delete the session
                    await authDbContext.Sessions
                        .Where(s => s.Id == sessionId &&
                            s.AccountId == accountId)
                        .ExecuteDeleteAsync();

                    return JsonResponseBuilder.Success(HttpStatusCode.NoContent);
                default:
                    logger.LogError("The result of the password verification is out of the range. Result: {Result}",
                        (int)passwordVerificationResult);

                    return JsonResponseBuilder.Error(
                        HttpStatusCode.InternalServerError,
                        errors: [
                            CommonErrors.UnknownError
                        ]
                    );
            }
        }
    }
}
