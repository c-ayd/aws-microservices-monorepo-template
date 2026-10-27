using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shared.Logging;
using Shared.Logging.Middlewares;
using Shared.Test.Generators;
using Shared.Test.Helpers.Fixtures;

namespace Shared.Test.Unit.Logging.Middlewares
{
    public class LoggingMiddlewareTest : IClassFixture<LoggerFixture<MetricsLoggingMiddleware>>
    {
        private readonly LoggerFixture<MetricsLoggingMiddleware> _loggerFixture;

        public LoggingMiddlewareTest(LoggerFixture<MetricsLoggingMiddleware> loggerFixture)
        {
            _loggerFixture = loggerFixture;
        }

        [Theory]
        [InlineData(null)]
        [InlineData("/health")]
        public async Task Invoke_WhenRequestIsNotHealthEndpoint_ShouldLog(string? healthEndpoint)
        {
            // Arrange
            _loggerFixture.Logs.Clear();

            var middleware = new MetricsLoggingMiddleware(
                async (context) => {},
                _loggerFixture);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions<JsonOptions>();
            var httpContext = new DefaultHttpContext()
            {
                RequestServices = services.BuildServiceProvider()
            };

            httpContext.Request.Path = "/" + StringGenerator.GenerateAlpha(healthEndpoint != null ? healthEndpoint.Length + 1 : 10);

            // Act
            await middleware.Invoke(httpContext);

            // Assert
            Assert.Single(_loggerFixture.Logs);
        }

        [Fact]
        public async Task Invoke_WhenRequestIsHealthEndpoint_ShouldNotLog()
        {
            // Arrange
            _loggerFixture.Logs.Clear();
            
            LoggingConfiguration.HealthEndpoint = "/health";

            var middleware = new MetricsLoggingMiddleware(
                async (context) => { },
                _loggerFixture);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions<JsonOptions>();
            var httpContext = new DefaultHttpContext()
            {
                RequestServices = services.BuildServiceProvider()
            };

            httpContext.Request.Path = LoggingConfiguration.HealthEndpoint;

            // Act
            await middleware.Invoke(httpContext);

            // Assert
            Assert.Empty(_loggerFixture.Logs);
        }

        [Theory]
        [InlineData("/.well-known/openid-configuration")]
        [InlineData("/.well-known/jwks.json")]
        public async Task Invoke_WhenRequestIsWellKnownEndpoint_ShouldNotLog(string endpoint)
        {
            // Arrange
            _loggerFixture.Logs.Clear();

            var middleware = new MetricsLoggingMiddleware(
                async (context) => { },
                _loggerFixture);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions<JsonOptions>();
            var httpContext = new DefaultHttpContext()
            {
                RequestServices = services.BuildServiceProvider()
            };

            httpContext.Request.Path = endpoint;

            // Act
            await middleware.Invoke(httpContext);

            // Assert
            Assert.Empty(_loggerFixture.Logs);
        }
    }
}
