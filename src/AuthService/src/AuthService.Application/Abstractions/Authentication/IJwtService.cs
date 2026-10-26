using System.Security.Claims;
using AuthService.Application.Dtos.Authentication;

namespace AuthService.Application.Abstractions.Authentication
{
    /// <summary>
    /// Provides a method to generate tokens for authentication and authorization.
    /// </summary>
    public interface IJwtService
    {
        /// <summary>
        /// Generates an access token and a refresh token.
        /// </summary>
        /// <param name="claims">Claims to be added in the access token</param>
        /// <param name="notBefore">Date time to activate the access token. <see cref="null"/> means <see cref="DateTimeOffset.UtcNow"/></param>
        /// <returns>Returns both access and refresh tokens along with their expiration dates.</returns>
        JwtDto GenerateTokens(IEnumerable<Claim>? claims = null, DateTimeOffset? notBefore = null);
    }
}
