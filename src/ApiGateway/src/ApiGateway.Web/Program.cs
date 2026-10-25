using System.Net.Security;
using System.Reflection;
using ApiGateway.Web.BackgroundServices;
using ApiGateway.Web.DistributedCaches;
using ApiGateway.Web.Middlewares;
using ApiGateway.Web.Options;
using ApiGateway.Web.Transforms.Request;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Shared.AspNetCore.Helpers.DependencyInjection;
using Shared.AspNetCore.Helpers.Kestrel;
using Shared.AspNetCore.Helpers.Kestrel.Structures;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;
using Shared.Aws.Helpers.DependencyInjection;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response.Middlewares;
using Shared.Logging.DependencyInjection;
using Shared.Logging.Middlewares;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAwsOptions(builder.Environment.IsProduction());
builder.Services.AddAwsSecretsManager();
await builder.Configuration.AddAwsSecretsAsync([
    "template/api-gateway/connections",
    "template/api-gateway/certificates",
    "template/cert/ca-bundle"
]);

var portConfigs = new PortConfigs();
var certificatesOptions = builder.Configuration.GetSection(CertificatesOptions.Key).Get<CertificatesOptions>()!;
builder.WebHost.ConfigureKestrel(options =>
{
    // Since YARP acts like an API gateway, it onyl accepts HTTP/1.1.
    // gRPC is used only for internal service communication.
    options.ConfigureHttps(
        name: "HttpsOnly",
        certificatesOptions.ServerKey,
        certificatesOptions.ServerCertificate,
        port: builder.Configuration.GetValue<int>("HttpsPort"),
        HttpProtocols.Http1,
        portConfigs);
});
builder.Services.AddSingleton(portConfigs);

builder.Services.AddSingleton<TokenBlacklistRedis>();
builder.Services.AddSingleton<RateLimiterRedis>();
builder.Services.AddHealthChecks();

var caBundleOptions = builder.Configuration.GetSection(CABundleOptions.Key).Get<CABundleOptions>()!;
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<ClearReservedHeadersTransform>()
    .AddTransforms<JwtBearerTransform>()
    .ConfigureHttpClient((context, handler) =>
    {
        var caBundle = TlsUtility.LoadCertificateChain(caBundleOptions.Value);
        handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
        {
            return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngularWeb", policy =>
    {
        policy.WithOrigins(builder.Configuration.GetValue<string>("CorsOrigins:AngularWeb")!)
            .AllowCredentials()
            .AllowAnyMethod()
            .AllowAnyHeader();
    });
});
builder.Services.AddHsts(options =>
{
    options.MaxAge = TimeSpan.FromDays(365);
    options.IncludeSubDomains = builder.Environment.IsProduction();
});

builder.Services.AddHostedService<RedisBackgroundServices>();

builder.RegisterOptionsFromAssembly(Assembly.GetExecutingAssembly());

var jwtOptions = builder.Configuration.GetSection(JwtOptions.Key).Get<JwtOptions>()!;
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = jwtOptions.Authority;
        options.RequireHttpsMetadata = true;

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters()
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            RequireSignedTokens = true,
            
            ValidIssuer = jwtOptions.Authority,
            ValidAudience = jwtOptions.Audience,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            ClockSkew = TimeSpan.FromMinutes(5), // Same as the access token lifespan

            NameClaimType = ApiGatewayAuthKeys.Claims.Id.ClaimType,
            RoleClaimType = ApiGatewayAuthKeys.Claims.Roles.ClaimType
        };

        var caBundle = TlsUtility.LoadCertificateChain(caBundleOptions.Value);
        options.BackchannelHttpHandler = new SocketsHttpHandler
        {
            SslOptions = new SslClientAuthenticationOptions()
            {
                RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                }
            }
        };
    });

builder.Services.AddAuthorization(config =>
{
    config.AddPolicy("Authenticated", policy => policy.RequireAuthenticatedUser());
    config.AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
});

builder.Logging.AddStructuredConsoleLogging(
    builder.Environment.IsProduction(),
    healthEndpoint: "/health");

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseHsts();

app.UseCors("AllowAngularWeb");

app.UseMiddleware<MetricsLoggingMiddleware>();

app.UseMiddleware<AuthErrorResponseMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuthScopeLoggingMiddleware>();

app.UseMiddleware<RateLimiterMiddleware>();

app.MapReverseProxy();

app.MapHealthChecks("/health");

app.Run();
