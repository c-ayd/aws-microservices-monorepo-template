using System.Reflection;
using Common.AspNetCore.Helpers.DependencyInjection;
using Common.Logging.DependencyInjection;
using Common.AspNetCore.Helpers.Kestrel;
using Common.AspNetCore.Helpers.Kestrel.Structures;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Common.Aws.Helpers.DependencyInjection;
using Common.RabbitMq.Helpers.EntityFramework.DbContexts;
using Common.PostgreSql.Helpers.EntityFramework.Extensions;
using RecoveryService.Worker.Options;
using RecoveryService.Worker.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAwsOptions(builder.Environment.IsProduction());
builder.Services.AddAwsSecretsManager();
builder.Services.AddAwsS3Bucket();
builder.Services.AddAwsKms();
await builder.Configuration.AddAwsSecretsAsync([
    "template/recovery-svc/connections",
    "template/cert/ca-bundle"
]);

var portConfigs = new PortConfigs();
builder.WebHost.ConfigureKestrel(options =>
{
    // Recovery service does not have to talk with any other services.
    // Its job is to handle dropped messages for RabbitMQ. The only
    // exposed endpoint is the health check endpoint, which can be
    // plain HTTP instead of HTTPS.
    options.ConfigureHttp(
        name: "Http",
        builder.Configuration.GetValue<int>("HttpPort"),
        HttpProtocols.Http1,
        portConfigs);
});
builder.Services.AddSingleton(portConfigs);

builder.Services.AddHostedService<DatabaseBackgroundService>();
builder.Services.AddHostedService<StorageBackgroundService>();

var connectionsOptions = builder.Configuration.GetSection(ConnectionsOptions.Key).Get<ConnectionsOptions>()!;
var caBudleOptions = builder.Configuration.GetSection(CABundleOptions.Key).Get<CABundleOptions>()!;
builder.Services.AddDbContext<DroppedMessagesDbContext>(builder =>
{
    builder.ConfigureDbContext(connectionsOptions.DroppedMessagesDb, caBudleOptions.Value);
});

builder.Services.AddHealthChecks();

builder.RegisterOptionsFromAssembly(Assembly.GetExecutingAssembly());

builder.Logging.AddStructuredConsoleLogging(
    builder.Environment.IsProduction(),
    healthEndpoint: "/health");

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();
