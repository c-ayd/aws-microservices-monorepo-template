using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthService.Application.Abstractions.Authentication;
using AuthService.Application.Dtos.Authentication;
using AuthService.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Crypto;

namespace AuthService.Infrastructure.Authentication
{
    public class JwtService : IJwtService
    {
        private readonly JwtOptions _jwtOptions;
        private readonly IJwtKeyService _jwtKeyService;

        public JwtService(
            IOptions<JwtOptions> jwtOptions,
            IJwtKeyService jwtKeyService)
        {
            _jwtOptions = jwtOptions.Value;
            _jwtKeyService = jwtKeyService;
        }

        public JwtDto GenerateTokens(ICollection<Claim>? claims = null, DateTimeOffset? notBefore = null)
        {
            var now = DateTimeOffset.UtcNow;
            var accessTokenExpirationDate = now.AddMinutes(_jwtOptions.AccessTokenLifespanInMinutes);
            var refreshTokenExpirationDate = now.AddDays(_jwtOptions.RefreshTokenLifespanInDays);

            var privateKey = _jwtKeyService.GetActivePrivateKey();
            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims ?? Enumerable.Empty<Claim>(),
                notBefore: notBefore?.UtcDateTime,
                expires: accessTokenExpirationDate.UtcDateTime,
                signingCredentials: new SigningCredentials(privateKey.Key, SecurityAlgorithms.RsaSha256)
            );
            token.Header["kid"] = privateKey.Id;

            return new JwtDto(
                AccessToken: new JwtSecurityTokenHandler().WriteToken(token),
                RefreshToken: TokenGenerator.GenerateBase64UrlSafe(),
                AccessTokenExpirationDate: accessTokenExpirationDate,
                RefreshTokenExpirationDate: refreshTokenExpirationDate
            );
        }
    }
}
