using System.Reflection;
using NotificationService.Worker.Abstractions;
using NotificationService.Worker.DbContexts;
using NotificationService.Worker.Options;
using NotificationService.Worker.Services;
using NotificationService.Worker.BackgroundServices;
using Shared.AspNetCore.Helpers.DependencyInjection;
using Shared.Logging.DependencyInjection;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Shared.PostgreSql.Helpers.EntityFramework.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    // Notification service does not have to talk with any other services.
    // Its job is to handle RabbitMQ messages. The only exposed endpoint is
    // the health check endpoint, which can be plain HTTP instead of HTTPS.
    
    options.ListenAnyIP(8080, config =>
    {
        config.Protocols = HttpProtocols.Http1;
    });
});

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
