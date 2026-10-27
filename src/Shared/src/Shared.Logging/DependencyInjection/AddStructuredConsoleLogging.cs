using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Shared.Logging.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static void AddStructuredConsoleLogging(this ILoggingBuilder logging, bool isProduction, string? healthEndpoint = null)
        {
            LoggingConfiguration.HealthEndpoint = healthEndpoint;

            logging.ClearProviders();
            logging.AddJsonConsole(config =>
            {
                config.IncludeScopes = true;
                config.UseUtcTimestamp = true;
                config.TimestampFormat = "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffffff'Z'";
                config.JsonWriterOptions = new JsonWriterOptions()
                {
                    Indented = !isProduction,
                    IndentSize = 2,
                    SkipValidation = isProduction,
                };
            });

            logging.AddFilter("Default", LogLevel.Information);
            logging.AddFilter("Microsoft.AspNetCore", LogLevel.Warning);
            if (isProduction)
            {
                logging.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            }
        }
    }
}
