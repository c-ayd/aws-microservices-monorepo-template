using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
using Shared.AspNetCore.Helpers.Tls.Options.Extensions;
using Shared.Constants;
using Shared.Http.Authentication.Constants;
using Shared.Redis.Options.Extensions;
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

        private const string _serviceKeyFilePath = "Certs/test-webapi-server.key";
        private const string _serviceCertificateFilePath = "Certs/test-webapi-server.crt";

        public RedisFixture TokenBlacklistRedisFixture { get; private set; }
        public RedisFixture RateLimiterRedis { get; private set; }
        public WebApiFixture AuthServiceFixture { get; private set; }
        public WebApiFixture ProtectedServiceFixture { get; private set; }
        public WebApiFixture AdminServiceFixture { get; private set; }

        public ApiGatewayWebAppFactory ApiGatewayWebApp { get; private set; } = null!;

        public ApiGatewayCollectionCluster()
        {
            TokenBlacklistRedisFixture = new RedisFixture();
            RateLimiterRedis = new RedisFixture();
            AuthServiceFixture = new WebApiFixture(
                port: 7000,
                caBundlePath: CABundlePath,
                serverKeyFilePath: _serviceKeyFilePath,
                serverCertificatePath: _serviceCertificateFilePath);
            ProtectedServiceFixture = new WebApiFixture(
                port: 7001,
                caBundlePath: CABundlePath,
                serverKeyFilePath: _serviceKeyFilePath,
                serverCertificatePath: _serviceCertificateFilePath);
            AdminServiceFixture = new WebApiFixture(
                port: 7002,
                caBundlePath: CABundlePath,
                serverKeyFilePath: _serviceKeyFilePath,
                serverCertificatePath: _serviceCertificateFilePath);
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                TokenBlacklistRedisFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    redisCertificateFolder: "Certs/Redis"),
                RateLimiterRedis.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    redisCertificateFolder: "Certs/Redis"),
                AuthServiceFixture.InitializeAsync(
                    addConfiguration: null,
                    configureServices: null,
                    configureApp: null,
                    configureEndpoints: DownstreamServiceConfigureEndpoints),
                ProtectedServiceFixture.InitializeAsync(
                    addConfiguration: null,
                    configureServices: null,
                    configureApp: null,
                    configureEndpoints: (endpoints) =>
                    {
                        endpoints.MapGet("/", () => Results.NoContent());
                    }),
                AdminServiceFixture.InitializeAsync(
                    addConfiguration: null,
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

            ApiGatewayWebApp = new ApiGatewayWebAppFactory(
                new ConnectionsOptions()
                {
                    TokenBlacklistRedis = TokenBlacklistRedisFixture.GetConnectionOptions(),
                    RateLimiterRedis = RateLimiterRedis.GetConnectionOptions()
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
                : base(port: 8443, caBundlePath: CABundlePath)
            {
                _connectionsOptions = connectionsOptions;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                base.ConfigureWebHost(builder);

                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddConfiguration(ConfigurationHelper.CreateConfigurationFromFile("appsettings.Extra.json"));

                    config.AddInMemoryCollection([
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Host)}",
                            _connectionsOptions.TokenBlacklistRedis.Host),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Port)}",
                            _connectionsOptions.TokenBlacklistRedis.Port.ToString()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Password)}",
                            _connectionsOptions.TokenBlacklistRedis.Password),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.AbortOnConnectionFail)}",
                            _connectionsOptions.TokenBlacklistRedis.AbortOnConnectionFail.ToString().ToLower()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TokenBlacklistRedis)}:{nameof(RedisConnectionOptions.Ssl)}:{nameof(SslOptions.CABundlePath)}",
                            _connectionsOptions.TokenBlacklistRedis.Ssl!.CABundlePath),
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
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RateLimiterRedis)}:{nameof(RedisConnectionOptions.Ssl)}:{nameof(SslOptions.CABundlePath)}",
                            _connectionsOptions.RateLimiterRedis.Ssl!.CABundlePath),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RateLimiterRedis)}:{nameof(RedisConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                            _connectionsOptions.RateLimiterRedis.Ssl!.ServerName)
                    ]);
                });
            }
        }

        private void DownstreamServiceConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            // Well known endpoints
            endpoints.MapGet("/.well-known/openid-configuration", async (context) =>
            {
                await Results.Ok(new
                {
                    issuer = "https://localhost:7000",
                    jwks_uri = "https://localhost:7000/.well-known/jwks.json"
                }).ExecuteAsync(context);
            });
            endpoints.MapGet("/.well-known/jwks.json", async (context) =>
            {
                var publicPem = await File.ReadAllTextAsync("./test_jwt_k1_public.txt");
                var rsa = RSA.Create();
                rsa.ImportFromPem(publicPem);
                var k1Parameters = rsa.ExportParameters(false);

                publicPem = await File.ReadAllTextAsync("./test_jwt_k2_public.txt");
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

                var privatePem = await File.ReadAllTextAsync("./test_jwt_k2_private.txt");
                var rsa = RSA.Create();
                rsa.ImportFromPem(privatePem);
                var key = new RsaSecurityKey(rsa)
                {
                    KeyId = "k2"
                };

                var token = new JwtSecurityToken(
                    issuer: "https://localhost:7000",
                    audience: "https://localhost:8443",
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
