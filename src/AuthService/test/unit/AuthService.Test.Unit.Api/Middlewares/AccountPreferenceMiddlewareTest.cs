using System.Security.Claims;
using AuthService.Api.Middlewares;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Common.Constants;
using Common.Http.Authentication.Constants;

namespace AuthService.Test.Unit.Api.Middlewares
{
    public class AccountPreferenceMiddlewareTest
    {
        private readonly AccountPreferenceMiddleware _middleware;

        public AccountPreferenceMiddlewareTest()
        {
            _middleware = new AccountPreferenceMiddleware(async (context) => {});
        }

        [Theory]
        [InlineData(SupportedLanguages.German, SupportedLanguages.German)]
        [InlineData("Test", SupportedLanguages.DefaultLanguage)]
        public async Task Invoke_WhenAuthenticatedAndUserPreferencesExist_ShouldAddPreferencesToItems(string language, string expected)
        {
            // Arrange
            var httpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                    claims: new List<Claim>()
                    {
                        new Claim(ApiGatewayAuthKeys.Claims.PreferredLanguage.ClaimType, language)
                    },
                    authenticationType: "TestAuth"))
            };

            // Act
            await _middleware.Invoke(httpContext);

            // Assert
            Assert.NotNull(httpContext.Items["PreferredLanguage"]);
            Assert.Equal(expected, (string)httpContext.Items["PreferredLanguage"]!);
        }

        [Fact]
        public async Task Invoke_WhenNotAuthenticatedAndUserPreferencesExist_ShouldAddPreferencesToItems()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();
            httpContext.Request.Headers.Append(HeaderNames.AcceptLanguage, "de-DE;q=0.9,en-US;q=0.8,en;q=0.7");

            // Act
            await _middleware.Invoke(httpContext);

            // Assert
            Assert.NotNull(httpContext.Items["PreferredLanguage"]);
            Assert.Equal(SupportedLanguages.German, (string)httpContext.Items["PreferredLanguage"]!);
        }

        [Fact]
        public async Task Invoke_WhenNotAuthenticatedAndUserPreferencesDoNotExist_ShouldAddNothingOrDefaultsToItems()
        {
            // Arrange
            var httpContext = new DefaultHttpContext();

            // Act
            await _middleware.Invoke(httpContext);

            // Assert
            Assert.NotNull(httpContext.Items["PreferredLanguage"]);
            Assert.Equal(SupportedLanguages.DefaultLanguage, (string)httpContext.Items["PreferredLanguage"]!);
        }
    }
}
