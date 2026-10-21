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
        private const string _caBundlePath = "Certs/test-ca-bundle.crt";
        private const string _apiGatewayServerKeyFilePath = "Certs/test-api-gateway-server.key";
        private const string _apiGatewayServerCertificateFilePath = "Certs/test-api-gateway-server.crt";
        private const string _webApiServerKeyFilePath = "Certs/WebApi/test-webapi-server.key";
        private const string _webApiServerCertificateFilePath = "Certs/WebApi/test-webapi-server.crt";

        private static int _awsPort;
        private static int _apiGatewayPort;
        private static int _authServicePort;
        private static int _protectedServicePort;
        private static int _adminServicePort;

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
                caBundlePath: _caBundlePath,
                serverKeyFilePath: _webApiServerKeyFilePath,
                serverCertificateFilePath: _webApiServerCertificateFilePath);
            ProtectedServiceFixture = new WebApiFixture(
                caBundlePath: _caBundlePath,
                serverKeyFilePath: _webApiServerKeyFilePath,
                serverCertificateFilePath: _webApiServerCertificateFilePath);
            AdminServiceFixture = new WebApiFixture(
                caBundlePath: _caBundlePath,
                serverKeyFilePath: _webApiServerKeyFilePath,
                serverCertificateFilePath: _webApiServerCertificateFilePath);
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                AwsFixture.InitializeAsync(),
                TokenBlacklistRedisFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: _caBundlePath,
                    redisCertificateFolder: "Certs/Redis"),
                RateLimiterRedis.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: _caBundlePath,
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

            var tokenBlacklistConnectionOptions = TokenBlacklistRedisFixture.GetConnectionOptions();
            var rateLimiterRedisConnectionOptions = RateLimiterRedis.GetConnectionOptions();

            await AwsFixture.AddSecretStringAsync("template/api-gateway/connections", JsonSerializer.Serialize(
            new
            {
                Connections = new
                {
                    TokenBlacklistRedis = tokenBlacklistConnectionOptions,
                    RateLimiterRedis = rateLimiterRedisConnectionOptions
                }
            }));
            await AwsFixture.AddSecretStringAsync("template/certificates/ca-bundle", JsonSerializer.Serialize(
            new
            {
                CABundle = new
                {
                    CurrentId = "2026-10",
                    Bundles = new[]
                    {
                        new { Id = "2026-10", Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _caBundlePath)) },
                        new { Id = "2026-06", Value = "No Bundle" }
                    }
                }
            }));
            await AwsFixture.AddSecretStringAsync("template/certificates/api-gateway", JsonSerializer.Serialize(
            new
            {
                Certificate = new
                {
                    ServerKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _apiGatewayServerKeyFilePath)),
                    ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _apiGatewayServerCertificateFilePath))
                }
            }));

            _awsPort = AwsFixture.GetPort();
            _apiGatewayPort = KestrelHelper.GetFreePort();
            _authServicePort = AuthServiceFixture.GetPort(WebApiFixture.HttpsPortConfigName);
            _protectedServicePort = ProtectedServiceFixture.GetPort(WebApiFixture.HttpsPortConfigName);
            _adminServicePort = AdminServiceFixture.GetPort(WebApiFixture.HttpsPortConfigName);

            ApiGatewayWebApp = new ApiGatewayWebAppFactory(
                new ConnectionsOptions()
                {
                    TokenBlacklistRedis = tokenBlacklistConnectionOptions,
                    RateLimiterRedis = rateLimiterRedisConnectionOptions
                });
            ApiGatewayWebApp.StartServer();
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
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
            private readonly ConnectionsOptions _connectionsOptions;

            public ApiGatewayWebAppFactory(ConnectionsOptions connectionsOptions)
                : base(_caBundlePath)
            {
                _connectionsOptions = connectionsOptions;
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddConfiguration(ConfigurationHelper.CreateConfigurationFromFile("appsettings.Extra.json"));

                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpsPort",
                        _apiGatewayPort.ToString()),

                    new KeyValuePair<string, string?>($"{JwtOptions.Key}:{nameof(JwtOptions.Authority)}",
                        $"https://localhost:{_authServicePort}"),
                    new KeyValuePair<string, string?>($"{JwtOptions.Key}:{nameof(JwtOptions.Audience)}",
                        $"https://localhost:{_apiGatewayPort}"),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{_awsPort}"),

                    new KeyValuePair<string, string?>("ReverseProxy:Clusters:auth-cluster:Destinations:auth-service:Address",
                        $"https://localhost:{_authServicePort}"),
                    new KeyValuePair<string, string?>("ReverseProxy:Clusters:protected-cluster:Destinations:protected-service:Address",
                        $"https://localhost:{_protectedServicePort}"),
                    new KeyValuePair<string, string?>("ReverseProxy:Clusters:admin-cluster:Destinations:admin-service:Address",
                        $"https://localhost:{_adminServicePort}"),

                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Host)}",
                        _connectionsOptions.TokenBlacklistRedis.Host),
                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Port)}",
                        _connectionsOptions.TokenBlacklistRedis.Port.ToString()),
                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Password)}",
                        _connectionsOptions.TokenBlacklistRedis.Password),
                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.AbortOnConnectionFail)}",
                        _connectionsOptions.TokenBlacklistRedis.AbortOnConnectionFail.ToString().ToLower()),
                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                        _connectionsOptions.TokenBlacklistRedis.Ssl!.ServerName),

                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RateLimiterRedis)}:{nameof(RedisConnectionOptions.Host)}",
                        _connectionsOptions.RateLimiterRedis.Host),
                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RateLimiterRedis)}:{nameof(RedisConnectionOptions.Port)}",
                        _connectionsOptions.RateLimiterRedis.Port.ToString()),
                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RateLimiterRedis)}:{nameof(RedisConnectionOptions.Password)}",
                        _connectionsOptions.RateLimiterRedis.Password),
                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RateLimiterRedis)}:{nameof(RedisConnectionOptions.AbortOnConnectionFail)}",
                        _connectionsOptions.RateLimiterRedis.AbortOnConnectionFail.ToString().ToLower()),
                    new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RateLimiterRedis)}:{nameof(RedisConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                        _connectionsOptions.RateLimiterRedis.Ssl!.ServerName)
                ]);
            }
        }

        private void DownstreamServiceConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            // Well known endpoints
            endpoints.MapGet("/.well-known/openid-configuration", async (context) =>
            {
                await Results.Ok(new
                {
                    issuer = $"https://localhost:{_authServicePort}",
                    jwks_uri = $"https://localhost:{_authServicePort}/.well-known/jwks.json"
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
                            kid = "k1",
                            use = "sig",
                            alg = "RS256",
                            n = Base64UrlEncoder.Encode(k1Parameters.Modulus),
                            e = Base64UrlEncoder.Encode(k1Parameters.Exponent)
                        },
                        new
                        {
                            kty = "RSA",
                            kid = "k2",
                            use = "sig",
                            alg = "RS256",
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
                    new Claim(ApiGatewayAuthKeys.Claims.EmailVerified.ClaimType, emailVerified.HasValue ? emailVerified.ToString()!.ToLower() : false.ToString().ToLower()),
                    new Claim(ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType, preferredLang != null ? preferredLang : SupportedLanguages.DefaultLanguage),
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
                    KeyId = "k2"
                };

                var token = new JwtSecurityToken(
                    issuer: $"https://localhost:{_authServicePort}",
                    audience: $"https://localhost:{_apiGatewayPort}",
                    claims: claims,
                    expires: expired.HasValue && expired.Value ? DateTimeOffset.UtcNow.AddDays(-1).UtcDateTime : DateTimeOffset.UtcNow.AddDays(1).UtcDateTime,
                    signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256)
                );
                token.Header["kid"] = "k2";

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
