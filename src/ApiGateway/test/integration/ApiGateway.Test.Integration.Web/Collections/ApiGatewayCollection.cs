using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using ApiGateway.Web.Options;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using Shared.AspNetCore.Helpers.Kestrel.Options.Extensions;
using Shared.Constants;
using Shared.Http.Authentication.Constants;
using Shared.Redis.Helpers.Options.Extensions;
using Shared.Test.Helpers;
using Shared.Test.Helpers.Fixtures;

namespace ApiGateway.Test.Integration.Web.Collections
{
    [CollectionDefinition(nameof(ApiGatewayCollection))]
    public class ApiGatewayCollection : ICollectionFixture<ApiGatewayCollectionCluster>
    {
    }

    public class ApiGatewayCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        private const string _apiGatewayServerCertificatePath = "Certs/test-api-gateway-server.crt";
        private const string _apiGatewayServerCertificatePrivateKeyPath = "Certs/test-api-gateway-server.key";
        private const string _webApiServerCertificatePath = "Certs/WebApi/test-webapi-server.crt";
        private const string _webApiServerPrivateKeyPath = "Certs/WebApi/test-webapi-server.key";

        public static int AwsPort { get; private set; }
        public static int ApiGatewayPort { get; private set; }
        public static int AuthServicePort { get; private set; }
        public static int ProtectedServicePort { get; private set; }
        public static int AdminServicePort { get; private set; }

        public AwsFixture AwsFixture { get; private set; }
        public RedisFixture TokenBlacklistRedisFixture { get; private set; }
        public RedisFixture RateLimiterRedis { get; private set; }
        public WebApiFixture AuthServiceFixture { get; private set; }
        public WebApiFixture ProtectedServiceFixture { get; private set; }
        public WebApiFixture AdminServiceFixture { get; private set; }

        public ApiGatewayWebAppFactory ApiGatewayWebApp { get; private set; } = null!;

        public ApiGatewayCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            TokenBlacklistRedisFixture = new RedisFixture();
            RateLimiterRedis = new RedisFixture();
            AuthServiceFixture = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                serverCertificatePath: _webApiServerCertificatePath,
                serverCertificatePrivateKeyPath: _webApiServerPrivateKeyPath);
            ProtectedServiceFixture = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                serverCertificatePath: _webApiServerCertificatePath,
                serverCertificatePrivateKeyPath: _webApiServerPrivateKeyPath);
            AdminServiceFixture = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                serverCertificatePath: _webApiServerCertificatePath,
                serverCertificatePrivateKeyPath: _webApiServerPrivateKeyPath);
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                AwsFixture.InitializeAsync(),
                TokenBlacklistRedisFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    redisCertificateFolder: "Certs/Redis"),
                RateLimiterRedis.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    redisCertificateFolder: "Certs/Redis"),
                AuthServiceFixture.InitializeAsync(
                    overrideConfiguration: null,
                    configureServices: null,
                    configureApp: null,
                    configureEndpoints: DownstreamServiceConfigureEndpoints),
                ProtectedServiceFixture.InitializeAsync(
                    overrideConfiguration: null,
                    configureServices: null,
                    configureApp: null,
                    configureEndpoints: (endpoints) =>
                    {
                        endpoints.MapGet("/", () => Results.NoContent());
                    }),
                AdminServiceFixture.InitializeAsync(
                    overrideConfiguration: null,
                    configureServices: null,
                    configureApp: null,
                    configureEndpoints: (endpoints) =>
                    {
                        endpoints.MapGet("/", () => Results.NoContent());
                    })
            );

            AuthServiceFixture.StartServer();
            ProtectedServiceFixture.StartServer();
            AdminServiceFixture.StartServer();

            await SetupAwsAsync();

            AwsPort = AwsFixture.GetPort();
            ApiGatewayPort = KestrelHelper.GetFreePort();
            AuthServicePort = AuthServiceFixture.GetPort(WebApiFixture.HttpsPortConfigName);
            ProtectedServicePort = ProtectedServiceFixture.GetPort(WebApiFixture.HttpsPortConfigName);
            AdminServicePort = AdminServiceFixture.GetPort(WebApiFixture.HttpsPortConfigName);

            ApiGatewayWebApp = new ApiGatewayWebAppFactory();
            ApiGatewayWebApp.StartServer();
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                AwsFixture.DisposeAsync(),
                TokenBlacklistRedisFixture.DisposeAsync(),
                RateLimiterRedis.DisposeAsync(),
                AuthServiceFixture.DisposeWebApiAsync(),
                ProtectedServiceFixture.DisposeWebApiAsync(),
                AdminServiceFixture.DisposeWebApiAsync()
            );

            await ApiGatewayWebApp.DisposeAsync();
        }

        public class ApiGatewayWebAppFactory : WebAppFactoryFixture<Program>
        {
            public ApiGatewayWebAppFactory() : base(CABundlePath)
            {
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddConfiguration(ConfigurationHelper.CreateConfigurationFromFile("appsettings.Extra.json"));

                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpsPort",
                        ApiGatewayPort.ToString()),

                    new KeyValuePair<string, string?>($"{JwtOptions.Key}:{nameof(JwtOptions.Authority)}",
                        $"https://localhost:{AuthServicePort}"),
                    new KeyValuePair<string, string?>($"{JwtOptions.Key}:{nameof(JwtOptions.Audience)}",
                        $"https://localhost:{ApiGatewayPort}"),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{AwsPort}"),

                    new KeyValuePair<string, string?>("ReverseProxy:Clusters:auth-cluster:Destinations:auth-service:Address",
                        $"https://localhost:{AuthServicePort}"),
                    new KeyValuePair<string, string?>("ReverseProxy:Clusters:protected-cluster:Destinations:protected-service:Address",
                        $"https://localhost:{ProtectedServicePort}"),
                    new KeyValuePair<string, string?>("ReverseProxy:Clusters:admin-cluster:Destinations:admin-service:Address",
                        $"https://localhost:{AdminServicePort}")
                ]);
            }
        }

        private async Task SetupAwsAsync()
        {
            await AwsFixture.SecretsManager_AddStringAsync("template/api-gateway/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        TokenBlacklistRedis = TokenBlacklistRedisFixture.GetConnectionOptions(),
                        RateLimiterRedis = RateLimiterRedis.GetConnectionOptions()
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/api-gateway/certificates", JsonSerializer.Serialize(
                new
                {
                    Certificate = new
                    {
                        ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _apiGatewayServerCertificatePath)),
                        ServerPrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _apiGatewayServerCertificatePrivateKeyPath))
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
                new
                {
                    CABundle = new
                    {
                        Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, CABundlePath))
                    }
                }));
        }

        private void DownstreamServiceConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            // Well known endpoints
            endpoints.MapGet("/.well-known/openid-configuration", async (context) =>
            {
                await Results.Ok(new
                {
                    issuer = $"https://localhost:{AuthServicePort}",
                    jwks_uri = $"https://localhost:{AuthServicePort}/.well-known/jwks.json"
                }).ExecuteAsync(context);
            });
            endpoints.MapGet("/.well-known/jwks.json", async (context) =>
            {
                var publicPem = await File.ReadAllTextAsync("./test-jwt-k1-public.pem");
                var rsa = RSA.Create();
                rsa.ImportFromPem(publicPem);
                var k1Parameters = rsa.ExportParameters(false);

                publicPem = await File.ReadAllTextAsync("./test-jwt-k2-public.pem");
                rsa = RSA.Create();
                rsa.ImportFromPem(publicPem);
                var k2Parameters = rsa.ExportParameters(false);

                await Results.Ok(new
                {
                    keys = new[]
                    {
                        new
                        {
                            kty = "RSA",
                            kid = "v1",
                            use = "sig",
                            alg = "RS256",
                            exp = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                            nbf = DateTimeOffset.UtcNow.AddMonths(-3).ToUnixTimeSeconds(),
                            n = Base64UrlEncoder.Encode(k1Parameters.Modulus),
                            e = Base64UrlEncoder.Encode(k1Parameters.Exponent)
                        },
                        new
                        {
                            kty = "RSA",
                            kid = "v2",
                            use = "sig",
                            alg = "RS256",
                            exp = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds(),
                            nbf = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                            n = Base64UrlEncoder.Encode(k2Parameters.Modulus),
                            e = Base64UrlEncoder.Encode(k2Parameters.Exponent)
                        }
                    }
                }).ExecuteAsync(context);
            });

            // Test endpoins
            endpoints.MapGet("/", () => Results.NoContent());
            endpoints.MapGet("/test/generate-access-token", async (
                HttpContext context,
                Guid accountId,
                string[]? roles,
                bool? emailVerified,
                string? preferredLang,
                bool? issuedInPast,
                bool? expired) =>
            {
                var claims = new List<Claim>()
                {
                    new Claim(ApiGatewayAuthKeys.Claims.Id.ClaimType, accountId.ToString()),
                    new Claim(ApiGatewayAuthKeys.Claims.EmailVerified.ClaimType, emailVerified.HasValue ? 
                        emailVerified.ToString()!.ToLower() : false.ToString().ToLower()),
                    new Claim(ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType, preferredLang != null ? 
                        preferredLang : SupportedLanguages.DefaultLanguage),
                    new Claim(ApiGatewayAuthKeys.Claims.IssuedAt.ClaimType, issuedInPast.HasValue && issuedInPast.Value ?
                        DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds().ToString() :
                        DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds().ToString()),
                };

                if (roles != null)
                {
                    foreach (var role in roles)
                    {
                        claims.Add(new Claim(ApiGatewayAuthKeys.Claims.Roles.ClaimType, role));
                    }
                }

                var privatePem = await File.ReadAllTextAsync("./test-jwt-k2-private.pem");
                var rsa = RSA.Create();
                rsa.ImportFromPem(privatePem);
                var key = new RsaSecurityKey(rsa)
                {
                    KeyId = "v2"
                };

                var token = new JwtSecurityToken(
                    issuer: $"https://localhost:{AuthServicePort}",
                    audience: $"https://localhost:{ApiGatewayPort}",
                    claims: claims,
                    expires: expired.HasValue && expired.Value ? 
                        DateTimeOffset.UtcNow.AddDays(-1).UtcDateTime : 
                        DateTimeOffset.UtcNow.AddDays(1).UtcDateTime,
                    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
                );
                token.Header["kid"] = "v2";

                await Results.Content(new JwtSecurityTokenHandler().WriteToken(token), "text/plain")
                    .ExecuteAsync(context);
            });
            endpoints.MapGet("/test/get-headers", async (context) =>
            {
                var headers = new Dictionary<string, string?>();
                foreach (var userClaim in ApiGatewayAuthKeys.Claims.AllUserClaims)
                {
                    if (context.Request.Headers.TryGetValue(userClaim.HeaderKey, out var headerValue))
                    {
                        headers.Add(userClaim.HeaderKey, headerValue.ToString());
                    }
                    else
                    {
                        headers.Add(userClaim.HeaderKey, null);
                    }
                }

                if (context.Request.Headers.TryGetValue(HeaderNames.Authorization, out var token))
                {
                    headers.Add(HeaderNames.Authorization, token);
                }
                else
                {
                    headers.Add(HeaderNames.Authorization, null);
                }

                await Results.Ok(headers).ExecuteAsync(context);
            });
        }
    }
}
