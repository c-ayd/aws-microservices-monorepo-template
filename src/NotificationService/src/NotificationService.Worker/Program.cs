using System.Reflection;
using NotificationService.Worker.BackgroundServices;
using Shared.AspNetCore.Helpers.DependencyInjection;
using Shared.Logging.DependencyInjection;
using Shared.AspNetCore.Helpers.Kestrel;
using Shared.AspNetCore.Helpers.Kestrel.Structures;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Shared.Aws.Helpers.DependencyInjection;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAwsOptions(builder.Environment.IsProduction());
builder.Services.AddAwsSecretsManager();
builder.Services.AddAwsSes();
await builder.Configuration.AddAwsSecretsAsync([
    "template/notification-svc/connections",
    "template/notification-svc/secrets",
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

builder.Services.AddHealthChecks();

builder.Services.AddHostedService<EmailBackgroundService>();

builder.RegisterOptionsFromAssembly(Assembly.GetExecutingAssembly());

builder.Logging.AddStructuredConsoleLogging(
    builder.Environment.IsProduction(),
    healthEndpoint: "/health");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
