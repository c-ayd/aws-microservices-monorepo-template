using System.Reflection;
using NotificationService.Worker.Abstractions;
using NotificationService.Worker.DbContexts;
using NotificationService.Worker.Options;
using NotificationService.Worker.Services;
using NotificationService.Worker.BackgroundServices;
using Shared.AspNetCore.Helpers.DependencyInjection;
using Shared.Logging.DependencyInjection;
using Shared.PostgreSql.Helpers.EntityFramework.Extensions;
using Shared.AspNetCore.Helpers.Kestrel;
using Shared.AspNetCore.Helpers.Kestrel.Structures;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Shared.Aws.Helpers.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAwsOptions(builder.Environment.IsProduction());
builder.Services.AddAwsSecretsManager();
await builder.Configuration.AddAwsSecrets([
    "template/notification-svc/connections",
    "template/cert/ca-bundle"
]);

var portConfigs = new PortConfigs();
builder.WebHost.ConfigureKestrel(options =>
{
    // Notification service does not have to talk with any other services.
    // Its job is to handle RabbitMQ messages. The only exposed endpoint is
    // the health check endpoint, which can be plain HTTP instead of HTTPS.
    options.ConfigureHttp(
        name: "Http",
        port: builder.Configuration.GetValue<int>("HttpPort"),
        HttpProtocols.Http1,
        portConfigs);
});
builder.Services.AddSingleton(portConfigs);

builder.Services.AddSingleton<IEmailService, SmtpService>();
builder.Services.AddSingleton<ITemplateService, TemplateService>();
builder.Services.AddDbContext<TemplateDbContext>(dbContextBuilder =>
{
    dbContextBuilder.ConfigureDbContext(builder.Configuration.GetSection(ConnectionsOptions.Key).Get<ConnectionsOptions>()!.TemplateDb,
        builder.Configuration.GetSection(CABundleOptions.Key).Get<CABundleOptions>()!.Value);
});
builder.Services.AddHealthChecks();

builder.Services.AddHostedService<TemplateBackgroundService>();
builder.Services.AddHostedService<EmailBackgroundService>();

builder.RegisterOptionsFromAssembly(Assembly.GetExecutingAssembly());

builder.Logging.AddStructuredConsoleLogging(
    builder.Environment.IsProduction(),
    healthEndpoint: "/health");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
