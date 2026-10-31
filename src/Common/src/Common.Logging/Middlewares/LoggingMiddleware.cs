using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Common.Logging.Middlewares
{
    public class LoggingMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<LoggingMiddleware> _logger;

        public LoggingMiddleware(
            RequestDelegate next,
            ILogger<LoggingMiddleware> logger)
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

        private void LogMetrics(ILogger<LoggingMiddleware> logger, string endpoint, int statusCode, double elapsedTimeMs)
        {
            if (endpoint.StartsWith("/.well-known") ||
                (LoggingConfiguration.HealthEndpoint != null && endpoint == LoggingConfiguration.HealthEndpoint))
                return;

            logger.LogInformation("Status Code: {StatusCode} - Elapsed Time: {ElapsedTime} ms",
                statusCode,
                elapsedTimeMs);
        }
    }
}
