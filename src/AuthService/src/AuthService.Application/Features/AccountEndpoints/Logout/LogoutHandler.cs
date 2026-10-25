using System.Net;
using System.Security.Cryptography;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shared.Crypto;
using Shared.Crypto.Exceptions;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response;

namespace AuthService.Application.Features.AccountEndpoints.Logout
{
    public class LogoutHandler
    {
        public static async Task<IResult> HandleAsync(
            HttpContext context,
            IHashVersions hashVersions,
            IAuthDbContext authDbContext)
        {
            // If the user does not have the cookie values, the session data is lost on the client side.
            // In this case, a response with the OK code is still returned, so that the client can log out on the frontend.
            var sessionId = context.Request.Cookies[CookieKeys.SessionId];
            var refreshToken = context.Request.Cookies[CookieKeys.RefreshToken];
            if (sessionId == null || refreshToken == null)
                return JsonResponseBuilder.Success(HttpStatusCode.NoContent);
            
            var sessionIdGuid = Guid.Parse(sessionId);
            var session = await authDbContext.Sessions
                .Where(s => s.Id == sessionIdGuid)
                .Select(s => new
                {
                    s.RefreshTokenHashed,
                    s.AccountId
                })
                .FirstOrDefaultAsync();

            // If the account ID and refresh tokens of the session do not match, do not delete the session. Nevertheless, 
            // a response with the OK code is still returned, so that the client can log out on the frontend.
            try
            {
                if (session == null ||
                    session.AccountId != Guid.Parse(context.User.Identity!.Name!) ||
                    !ValueHasher.Verify(session.RefreshTokenHashed, refreshToken, hashVersions.GetHashOptions, out var _))
                    return JsonResponseBuilder.Success(HttpStatusCode.NoContent);
            }
            catch (Exception exception) when (
                exception is CryptographicException ||
                exception is FormatException ||
                exception is HashLengthMismatchException)
            {
                return JsonResponseBuilder.Success(HttpStatusCode.NoContent);
            }

            // Delete the current session
            await authDbContext.Sessions
                .Where(s => s.Id == sessionIdGuid)
                .ExecuteDeleteAsync();

            return JsonResponseBuilder.Success(HttpStatusCode.NoContent);
        }
    }
}
