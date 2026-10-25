using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AuthService.Infrastructure.Authentication;
using AuthService.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Shared.Http.Authentication.Constants;
using Shared.Test.Generators;

namespace AuthService.Test.Unit.Infrastructure.Authentication
{
    public class JwtServiceTest
    {
        public static readonly JwtOptions _jwtOptions = new JwtOptions()
        {
            Issuer = "https://localhost:7000",
            Audience = "https://localhost:6000",
            AccessTokenLifespanInMinutes = 5,
            RefreshTokenLifespanInDays = 2
        };
        
        private readonly JwtKeyService _jwtKeyService;
        private readonly JwtService _jwtService;

        public JwtServiceTest()
        {
            _jwtKeyService = new JwtKeyService(Options.Create(JwtKeyServiceTest._jwtKeysOptions));
            _jwtService = new JwtService(Options.Create(_jwtOptions), _jwtKeyService);
        }

        private (List<Claim>?, DateTime?, DateTime?) DecodeAccessToken(string accessToken)
        {
            var handler = new JwtSecurityTokenHandler();
            var validationParams = new TokenValidationParameters()
            {
                ValidateAudience = true,
                ValidateIssuer = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,

                ValidAudience = _jwtOptions.Audience,
                ValidIssuer = _jwtOptions.Issuer,
                IssuerSigningKey = _jwtKeyService.GetActivePublicKey().Key,

                NameClaimType = ApiGatewayAuthKeys.Claims.Id.ClaimType,
                RoleClaimType = ApiGatewayAuthKeys.Claims.Roles.ClaimType
            };

            try
            {
                var claimsPrincipal = handler.ValidateToken(accessToken, validationParams, out var token);
                if (token is not JwtSecurityToken jwtToken)
                {
                    Assert.Fail("Validated token is not a JWT security token.");
                    return (null, null, null);
                }

                return (jwtToken.Claims.ToList(), jwtToken.ValidFrom, jwtToken.ValidTo);
            }
            catch (Exception exception)
            {
                Assert.Fail($"Validation Failed: {exception.Message}");
                return (null, null, null);
            }
        }

        [Fact]
        public void GenerateToken_WhenClaimsAndNotBeforeDateTimeAreNotGiven_ShouldGenerateToken()
        {
            // Arrange
            var accessTokenLifespan = _jwtOptions.AccessTokenLifespanInMinutes;
            var refreshTokenLifespan = _jwtOptions.RefreshTokenLifespanInDays * 24 * 60;
            var now = DateTimeOffset.UtcNow;

            // Act
            var result = _jwtService.GenerateTokens();

            // Assert
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);

            var accessTokenLifespanInMinutes = (result.AccessTokenExpirationDate - now).TotalMinutes;
            var refreshTokenLifespanInMinutes = (result.RefreshTokenExpirationDate - now).TotalMinutes;
            Assert.InRange(accessTokenLifespanInMinutes, accessTokenLifespan - 1, accessTokenLifespan + 1);
            Assert.InRange(refreshTokenLifespanInMinutes, refreshTokenLifespan - 1, refreshTokenLifespan + 1);
        }

        [Fact]
        public void GenerateJwtToken_WhenClaimsAreNotGivenButNotBeforeDateTimeIsGiven_ShouldGenerateToken()
        {
            // Arrange
            var accessTokenLifespan = _jwtOptions.AccessTokenLifespanInMinutes;
            var refreshTokenLifespan = _jwtOptions.RefreshTokenLifespanInDays * 24 * 60;
            var now = DateTimeOffset.UtcNow;

            var notBefore = now.AddMinutes(1);

            // Act
            var result = _jwtService.GenerateTokens(notBefore: notBefore);

            // Assert
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);

            var accessTokenLifespanInMinutes = (result.AccessTokenExpirationDate - now).TotalMinutes;
            var refreshTokenLifespanInMinutes = (result.RefreshTokenExpirationDate - now).TotalMinutes;
            Assert.InRange(accessTokenLifespanInMinutes, accessTokenLifespan - 1, accessTokenLifespan + 1);
            Assert.InRange(refreshTokenLifespanInMinutes, refreshTokenLifespan - 1, refreshTokenLifespan + 1);

            var (_, decodedNotBefore, _) = DecodeAccessToken(result.AccessToken);
            Assert.Equal(notBefore.ToString("dd-MM-yyyy HH:mm:ss"), decodedNotBefore!.Value.ToString("dd-MM-yyyy HH:mm:ss"));
        }

        [Fact]
        public void GenerateJwtToken_WhenClaimsAreGivenButNotBeforeDateTimeIsNotGiven_ShouldGenerateToken()
        {
            // Arrange
            var id = StringGenerator.GeneratePrintableAscii();
            var language = StringGenerator.GeneratePrintableAscii();

            var claims = new List<Claim>()
            {
                new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, id),
                new Claim(ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType, language)
            };

            var accessTokenLifespan = _jwtOptions.AccessTokenLifespanInMinutes;
            var refreshTokenLifespan = _jwtOptions.RefreshTokenLifespanInDays * 24 * 60;
            var now = DateTimeOffset.UtcNow;

            // Act
            var result = _jwtService.GenerateTokens(claims);

            // Assert
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);

            var accessTokenLifespanInMinutes = (result.AccessTokenExpirationDate - now).TotalMinutes;
            var refreshTokenLifespanInMinutes = (result.RefreshTokenExpirationDate - now).TotalMinutes;
            Assert.InRange(accessTokenLifespanInMinutes, accessTokenLifespan - 1, accessTokenLifespan + 1);
            Assert.InRange(refreshTokenLifespanInMinutes, refreshTokenLifespan - 1, refreshTokenLifespan + 1);

            var (decodedClaims, _, _) = DecodeAccessToken(result.AccessToken);
            var decodedId = decodedClaims!.Find(c => c.Type == ApiGatewayAuthKeys.Claims.Id.ClaimType)!.Value;
            var decodedLanguage = decodedClaims!.Find(c => c.Type == ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType)!.Value;
            Assert.Equal(id, decodedId);
            Assert.Equal(language, decodedLanguage);
        }

        [Fact]
        public void GenerateJwtToken_WhenClaimsAndNotBeforeDateTimeAreGiven_ShouldGenerateToken()
        {
            // Arrange
            var id = StringGenerator.GeneratePrintableAscii();
            var language = StringGenerator.GeneratePrintableAscii();

            var claims = new List<Claim>()
            {
                new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, id),
                new Claim(ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType, language)
            };

            var accessTokenLifespan = _jwtOptions.AccessTokenLifespanInMinutes;
            var refreshTokenLifespan = _jwtOptions.RefreshTokenLifespanInDays * 24 * 60;
            var now = DateTimeOffset.UtcNow;

            var notBefore = now.AddMinutes(1);

            // Act
            var result = _jwtService.GenerateTokens(claims, notBefore);

            // Assert
            Assert.NotNull(result.AccessToken);
            Assert.NotNull(result.RefreshToken);

            var accessTokenLifespanInMinutes = (result.AccessTokenExpirationDate - now).TotalMinutes;
            var refreshTokenLifespanInMinutes = (result.RefreshTokenExpirationDate - now).TotalMinutes;
            Assert.InRange(accessTokenLifespanInMinutes, accessTokenLifespan - 1, accessTokenLifespan + 1);
            Assert.InRange(refreshTokenLifespanInMinutes, refreshTokenLifespan - 1, refreshTokenLifespan + 1);

            var (decodedClaims, decodedNotBefore, _) = DecodeAccessToken(result.AccessToken);
            var decodedId = decodedClaims!.Find(c => c.Type == ApiGatewayAuthKeys.Claims.Id.ClaimType)!.Value;
            var decodedLanguage = decodedClaims!.Find(c => c.Type == ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType)!.Value;
            Assert.Equal(id, decodedId);
            Assert.Equal(language, decodedLanguage);
            Assert.Equal(notBefore.ToString("dd-MM-yyyy HH:mm:ss"), decodedNotBefore!.Value.ToString("dd-MM-yyyy HH:mm:ss"));
        }
    }
}
