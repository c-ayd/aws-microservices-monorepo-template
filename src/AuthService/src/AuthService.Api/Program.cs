using System.Reflection;
using AuthService.Api.BackgroundServices;
using AuthService.Infrastructure;
using AuthService.Persistence;
using Shared.Logging.DependencyInjection;
using Shared.AspNetCore.Helpers.DependencyInjection;
using AuthService.Api.WellKnown;
using Shared.Http.Authentication;
using Shared.Http.Response.Middlewares;
using Microsoft.AspNetCore.Authentication;
using AuthService.Api.Middlewares;
using Shared.Logging.Middlewares;
using AuthService.Application.Features.AccountEndpoints;
using AuthService.Application;
using Shared.Http.DependencyInjection;
using Shared.Http.Authentication.Constants;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Shared.AspNetCore.Helpers.Kestrel;
using Shared.AspNetCore.Helpers.Kestrel.Structures;
using Shared.Http.Request;
using Shared.Aws.Helpers.DependencyInjection;
using AuthService.Application.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAwsOptions(builder.Environment.IsProduction());
builder.Services.AddAwsSecretsManager();
builder.Services.AddAwsKms();
builder.Services.AddAwsS3Bucket();
await builder.Configuration.AddAwsSecrets([
    "template/auth-svc/connections",
    "template/auth-svc/certificates",
    "template/auth-svc/secrets",
    "template/cert/ca-bundle"
]);

var portConfigs = new PortConfigs();
builder.WebHost.ConfigureKestrel(options =>
{
    var certificatesOptions = builder.Configuration.GetSection(CertificatesOptions.Key).Get<CertificatesOptions>()!;
    options.ConfigureHttps(
        name: "HttpsOnly",
        certificatesOptions.ServerKey,
        certificatesOptions.ServerCertificate,
        port: builder.Configuration.GetValue<int>("HttpsPort"),
        HttpProtocols.Http1,
        portConfigs);
});
builder.Services.AddSingleton(portConfigs);

builder.Services.ConfigureForwardedHeaders(
    builder.Configuration.GetSection("KnownCidrBlocks").Get<string[]>()!);

builder.Services.AddPersistenceServices(builder.Configuration);
builder.Services.AddInfrastructureServices();
builder.Services.AddApplicationServices();

builder.Services.AddHealthChecks();

builder.Services.AddHostedService<RabbitMqPublisherBackgroundService>();
builder.Services.AddHostedService<RedisBackgroundServices>();

builder.RegisterOptionsFromAssemblies(Assembly.GetAssembly(typeof(AuthService.Application.ServiceRegistration))!);

builder.Services.AddValidatorsFromAssembly(Assembly.GetAssembly(typeof(AuthService.Application.ServiceRegistration))!);

builder.Services.AddAuthentication(ApiGatewayAuthKeys.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, ApiGatewayAuthHandler>(ApiGatewayAuthKeys.AuthenticationScheme, options => { });
builder.Services.AddAuthorization();

builder.Logging.AddStructuredConsoleLogging(
    builder.Environment.IsProduction(),
    healthEndpoint: "/health");

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<MetricsLoggingMiddleware>();

app.UseMiddleware<AuthErrorResponseMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuthScopeLoggingMiddleware>();

app.UseMiddleware<AccountPreferenceMiddleware>();

app.MapWellKnownEndpoints();
app.MapAccountEndpoints();

app.MapHealthChecks("/health");

app.Run();
