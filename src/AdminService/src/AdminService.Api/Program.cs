using System.Reflection;
using AdminService.Api.Middlewares;
using AdminService.Application;
using AdminService.Application.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Shared.AspNetCore.Helpers.DependencyInjection;
using Shared.AspNetCore.Helpers.Kestrel;
using Shared.AspNetCore.Helpers.Kestrel.Structures;
using Shared.Aws.Helpers.DependencyInjection;
using Shared.Http.Authentication;
using Shared.Http.Authentication.Constants;
using Shared.Http.DependencyInjection;
using Shared.Http.Request;
using Shared.Http.Response.Middlewares;
using Shared.Logging.DependencyInjection;
using Shared.Logging.Middlewares;

var builder = WebApplication.CreateBuilder(args);

await builder.Configuration.AddAwsSecretsAsync([
    "template/admin-svc/certificates",
    "template/cert/ca-bundle"
]);

var portConfigs = new PortConfigs();
var certificatesOptions = builder.Configuration.GetSection(CertificatesOptions.Key).Get<CertificatesOptions>()!;
builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttps(
        name: "HttpsOnly",
        certificatesOptions.ServerCertificate,
        certificatesOptions.ServerPrivateKey,
        builder.Configuration.GetValue<int>("HttpsPort"),
        HttpProtocols.Http1,
        portConfigs);
});
builder.Services.AddSingleton(portConfigs);

builder.Services.ConfigureForwardedHeaders(
    builder.Configuration.GetSection("KnownCidrBlocks").Get<string[]>()!);

builder.Services.AddApplicationServices();

builder.Services.AddHealthChecks();

builder.RegisterOptionsFromAssemblies(Assembly.GetAssembly(typeof(AdminService.Application.ServiceRegistration))!);

builder.Services.AddValidatorsFromAssembly(Assembly.GetAssembly(typeof(AdminService.Application.ServiceRegistration))!);

builder.Services.AddAuthentication(ApiGatewayAuthKeys.AuthenticationScheme)
    .AddScheme<AuthenticationSchemeOptions, ApiGatewayAuthHandler>(ApiGatewayAuthKeys.AuthenticationScheme, options => { });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy => policy.RequireRole("Admin"));
});

builder.Logging.AddStructuredConsoleLogging(
    builder.Environment.IsProduction(),
    healthEndpoint: "/health");

var app = builder.Build();

app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<LoggingMiddleware>();

app.UseMiddleware<AuthErrorResponseMiddleware>();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<AuthScopeLoggingMiddleware>();

app.MapHealthChecks("/health");

app.Run();
