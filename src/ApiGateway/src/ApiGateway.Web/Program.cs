using System.Reflection;
using System.Security.Cryptography.X509Certificates;
using ApiGateway.Web.BackgroundServices;
using ApiGateway.Web.DistributedCaches;
using ApiGateway.Web.Middlewares;
using ApiGateway.Web.Options;
using ApiGateway.Web.Transforms.Request;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.IdentityModel.Tokens;
using Shared.AspNetCore.Helpers.DependencyInjection;
using Shared.AspNetCore.Helpers.Tls;
using Shared.AspNetCore.Helpers.Tls.Utilities;
using Shared.Http.Authentication.Constants;
using Shared.Http.Response.Middlewares;
using Shared.Logging.DependencyInjection;
using Shared.Logging.Middlewares;

var builder = WebApplication.CreateBuilder(args);

var certificateOptions = builder.Configuration.GetSection(CertificateOptions.Key).Get<CertificateOptions>()!;
builder.WebHost.ConfigureKestrel(options =>
{
    var serverCertificate = TlsUtility.LoadCertificateFromFile(
        certificateOptions.ServerKeyFilePath,
        certificateOptions.ServerCertificatePath);

    // Since YARP acts like an API gateway, it onyl accepts HTTP/1.1.
    // gRPC is used only for internal service communication.
    options.ConfigureTls(serverCertificate, port: 8443, HttpProtocols.Http1);
});

builder.Services.AddSingleton<TokenBlacklistRedis>();
builder.Services.AddSingleton<RateLimiterRedis>();
builder.Services.AddHealthChecks();
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
    .AddTransforms<ClearReservedHeadersTransform>()
    .AddTransforms<JwtBearerTransform>()
    .ConfigureHttpClient((context, handler) =>
    {
        var caBundle = X509CertificateLoader.LoadCertificateFromFile(certificateOptions.CABundlePath);
        handler.SslOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
        {
            return TlsUtility.ValidateServerCertificate(certificate, caBundle);
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
        options.RequireHttpsMetadata = builder.Environment.IsProduction();

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

            NameClaimType = ApiGatewayAuthKeys.Claims.Id.ClaimType,
            RoleClaimType = ApiGatewayAuthKeys.Claims.Roles.ClaimType
        };

        options.BackchannelHttpHandler = new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
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

app.UseMiddleware<LoggingMiddleware>();

app.UseMiddleware<AuthErrorResponseMiddleware>();
app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<RateLimiterMiddleware>();

app.MapReverseProxy();

app.MapHealthChecks("/health");

app.Run();
