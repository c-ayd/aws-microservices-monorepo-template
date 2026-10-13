using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Shared.Logging.Middlewares
{
    public class MetricsLoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<MetricsLoggingMiddleware> _logger;

        public MetricsLoggingMiddleware(
            RequestDelegate next,
            ILogger<MetricsLoggingMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task Invoke(HttpContext context)
        {
            var startTime = Stopwatch.GetTimestamp();
            try
            {
                await _next(context);

                var elapsedTime = Stopwatch.GetElapsedTime(startTime);
                LogMetrics(_logger, context.Request.Path, context.Response.StatusCode, elapsedTime.TotalMilliseconds);
            }
            catch
            {
                var elapsedTime = Stopwatch.GetElapsedTime(startTime);
                LogMetrics(_logger, context.Request.Path, StatusCodes.Status500InternalServerError, elapsedTime.TotalMilliseconds);

                throw;
            }
        }

        private void LogMetrics(ILogger<MetricsLoggingMiddleware> logger, string endpoint, int statusCode, double elapsedTimeMs)
        {
            if (endpoint.StartsWith("/.well-known") ||
                (LoggingOptions.HealthEndpoint != null && endpoint == LoggingOptions.HealthEndpoint))
                return;

            logger.LogInformation("Status Code: {StatusCode} - Elapsed Time: {ElapsedTime} ms",
                statusCode,
                elapsedTimeMs);
        }
    }
}
