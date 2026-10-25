using AuthService.Application.Abstractions.Authentication;
using AuthService.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Api.WellKnown
{
    public static class WellKnownEndpoints
    {
        public static void MapWellKnownEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/.well-known");

            group.MapGet("/openid-configuration", (IOptions<JwtOptions> jwtOptions) =>
            {
                return Results.Ok(new
                {
                    issuer = jwtOptions.Value.Issuer,
                    jwks_uri = $"{jwtOptions.Value.Issuer}/.well-known/jwks.json"
                });
            });

            group.MapGet("/jwks.json", (IJwtKeyService jwtKeyService) =>
            {
                return Results.Ok(new
                {
                    keys = jwtKeyService.GetAllPublicKeys().Select(k => new
                    {
                        kty = "RSA",
                        kid = k.Id,
                        use = "sig",
                        alg = "RS256",
                        exp = k.ExpirationTime,
                        nbf = k.NotBeforeTime,
                        n = Base64UrlEncoder.Encode(k.Parameters!.Value.Modulus),
                        e = Base64UrlEncoder.Encode(k.Parameters.Value.Exponent)
                    }).ToList()
                });
            });
        }
    }
}
