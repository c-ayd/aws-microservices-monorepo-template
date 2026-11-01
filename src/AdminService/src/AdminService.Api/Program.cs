using System.Reflection;
using AdminService.Api.Middlewares;
using AdminService.Application;
using AdminService.Application.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Common.AspNetCore.Helpers.DependencyInjection;
using Common.AspNetCore.Helpers.Kestrel;
using Common.AspNetCore.Helpers.Kestrel.Structures;
using Common.Aws.Helpers.DependencyInjection;
using Common.Http.Authentication;
using Common.Http.Authentication.Constants;
using Common.Http.DependencyInjection;
using Common.Http.Request;
using Common.Http.Response.Middlewares;
using Common.Logging.DependencyInjection;
using Common.Logging.Middlewares;
using AdminService.Application.Features.AuthEndpoints;
using Common.Grpc.Helpers.DependencyInjection;
using Shared.Grpc.Auth.Admin;

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

var caBundleOptions = builder.Configuration.GetSection(CABundleOptions.Key).Get<CABundleOptions>()!;
builder.Services.AddGrpcClients(
    urlsAndClients: new Dictionary<string, Type>()
    {
        { builder.Configuration.GetValue<string>("Grpc:Auth")!, typeof(AuthGrpcAdmin.AuthGrpcAdminClient) }
    },
    caBundleOptions.Value);

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

app.MapAuthEndpoints();

app.MapHealthChecks("/health");

app.Run();
