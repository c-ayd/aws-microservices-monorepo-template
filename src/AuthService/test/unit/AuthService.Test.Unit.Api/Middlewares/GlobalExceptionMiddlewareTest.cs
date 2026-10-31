using AuthService.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Common.Test.Helpers.Fixtures;

namespace AuthService.Test.Unit.Api.Middlewares
{
    public class GlobalExceptionMiddlewareTest : IClassFixture<LoggerFixture<GlobalExceptionMiddleware>>
    {
        private readonly LoggerFixture<GlobalExceptionMiddleware> _loggerFixture;

        public GlobalExceptionMiddlewareTest(LoggerFixture<GlobalExceptionMiddleware> loggerFixture)
        {
            _loggerFixture = loggerFixture;
        }

        [Fact]
        public async Task Invoke_WhenBadRequestExceptionIsThrown_ShouldReturnBadRequest()
        {
            // Arrange
            var middleware = new GlobalExceptionMiddleware(
                async (context) => throw new BadHttpRequestException("Test exception"),
                _loggerFixture);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions<JsonOptions>();
            var httpContext = new DefaultHttpContext()
            {
                RequestServices = services.BuildServiceProvider()
            };

            // Act
            await middleware.Invoke(httpContext);

            // Assert
            Assert.Equal(StatusCodes.Status400BadRequest, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_WhenGlobalExceptionIsThrown_ShouldReturnInternalServerError()
        {
            // Arrange
            var middleware = new GlobalExceptionMiddleware(
                async (context) => throw new Exception("Test exception"),
                _loggerFixture);

            var services = new ServiceCollection();
            services.AddLogging();
            services.AddOptions<JsonOptions>();
            var httpContext = new DefaultHttpContext()
            {
                RequestServices = services.BuildServiceProvider()
            };

            // Act
            await middleware.Invoke(httpContext);

            // Assert
            Assert.Equal(StatusCodes.Status500InternalServerError, httpContext.Response.StatusCode);
        }

        [Fact]
        public async Task Invoke_WhenExceptionIsNotThrown_ShouldContinueExecution()
        {
            // Arrange
            var middleware = new GlobalExceptionMiddleware(
                async (context) => context.Response.StatusCode = StatusCodes.Status204NoContent,
                _loggerFixture);

            var httpContext = new DefaultHttpContext();

            // Act
            await middleware.Invoke(httpContext);

            // Assert
            Assert.Equal(StatusCodes.Status204NoContent, httpContext.Response.StatusCode);
        }
    }
}
