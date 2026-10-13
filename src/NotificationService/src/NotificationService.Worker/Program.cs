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

var builder = WebApplication.CreateBuilder(args);

var portConfigs = new PortConfigs();
builder.WebHost.ConfigureKestrel(options =>
{
    // Notification service does not have to talk with any other services.
    // Its job is to handle RabbitMQ messages. The only exposed endpoint is
    // the health check endpoint, which can be plain HTTP instead of HTTPS.
    options.ConfigureHttp(
        name: "Http",
        port: builder.Configuration.GetValue<int>("HttpPort"),
        portConfigs);
});
builder.Services.AddSingleton(portConfigs);

builder.Services.AddSingleton<IEmailService, SmtpService>();
builder.Services.AddSingleton<ITemplateService, TemplateService>();
builder.Services.AddDbContext<TemplateDbContext>(dbContextBuilder =>
{
    dbContextBuilder.ConfigureDbContext(builder.Configuration
        .GetSection(ConnectionsOptions.Key).Get<ConnectionsOptions>()!
        .TemplateDb);
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
