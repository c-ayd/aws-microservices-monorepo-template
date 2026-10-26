using System.Net;
using System.Net.Http.Json;
using System.Reflection;
using Shared.Http.DependencyInjection;
using Shared.Http.Response.Structures;
using Shared.Http.Validation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Shared.Test.Integration.Http.Collections;
using Shared.Test.Helpers.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Routing;

namespace Shared.Test.Integration.Http.DependencyInjection
{
    [Collection(nameof(TestHostCollection))]
    public class ValidationFiltersTest
    {
        public const string ErrorCode = "TestValue";

        private readonly TestHostCollectionCluster _collectionCluster;

        public ValidationFiltersTest(TestHostCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

#pragma warning disable xUnit1013 // Public method should be marked as test
        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());
        }

        public static void ConfigureEndpoints(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapPost("/valiation-filter/validation", 
                    (HttpContext context, ValidationType1 value) => Results.NoContent().ExecuteAsync(context))
                .AddValidation<ValidationType1>();
            endpoints.MapPost("/valiation-filter/async-validation",
                    (HttpContext context, ValidationType2 value) => Results.NoContent().ExecuteAsync(context))
                .AddAsyncValidation<ValidationType2>();
        }
#pragma warning restore xUnit1013 // Public method should be marked as test

        [Fact]
        public async Task AddValidation_WhenValidatorIsAddedAndValueIsCorrect_ShouldReturnNoContent()
        {
            // Arrange
            var request = new ValidationType1(10);

            using var client = _collectionCluster.WebApiFixture.CreateHttpClient(WebApiFixture.HttpsPortConfigName);

            // Act
            var response = await client.PostAsJsonAsync("/valiation-filter/validation", request);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task AddValidation_WhenValidatorIsAddedAndValueIsNotCorrect_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new ValidationType1(-1);

            using var client = _collectionCluster.WebApiFixture.CreateHttpClient(WebApiFixture.HttpsPortConfigName);

            // Act
            var response = await client.PostAsJsonAsync("/valiation-filter/validation", request);
            var jsonResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(jsonResponse);
            Assert.Single(jsonResponse.Errors);
            Assert.Equal(ErrorCode, jsonResponse.Errors[0].Code);
        }

        [Fact]
        public async Task AddAsyncValidation_WhenValidatorIsAddedAndValueIsCorrect_ShouldReturnNoContent()
        {
            // Arrange
            var request = new ValidationType1(10);

            using var client = _collectionCluster.WebApiFixture.CreateHttpClient(WebApiFixture.HttpsPortConfigName);

            // Act
            var response = await client.PostAsJsonAsync("/valiation-filter/async-validation", request);

            // Assert
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        }

        [Fact]
        public async Task AddAsyncValidation_WhenValidatorIsAddedAndValueIsNotCorrect_ShouldReturnBadRequest()
        {
            // Arrange
            var request = new ValidationType1(-1);

            using var client = _collectionCluster.WebApiFixture.CreateHttpClient(WebApiFixture.HttpsPortConfigName);

            // Act
            var response = await client.PostAsJsonAsync("/valiation-filter/async-validation", request);
            var jsonResponse = await response.Content.ReadFromJsonAsync<ErrorResponse>();

            // Assert
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.NotNull(jsonResponse);
            Assert.Single(jsonResponse.Errors);
            Assert.Equal(ErrorCode, jsonResponse.Errors[0].Code);
        }

        public record ValidationType1(int IntValue);
        public record ValidationType2(int IntValue);

        private class ErrorResponse
        {
            public List<ErrorItem> Errors { get; set; } = new List<ErrorItem>();
        }
    }

    public class ValidationFiltersTest_TestValidator : IValidator<ValidationFiltersTest.ValidationType1>
    {
        public List<ErrorItem> Validate(ValidationFiltersTest.ValidationType1 value)
        {
            var errors = new List<ErrorItem>();

            if (value.IntValue < 0)
            {
                errors.Add(new ErrorItem(ValidationFiltersTest.ErrorCode));
            }

            return errors;
        }
    }

    public class ValidationFiltersTest_TestAsyncValidator : IAsyncValidator<ValidationFiltersTest.ValidationType2>
    {
        public async Task<List<ErrorItem>> ValidateAsync(ValidationFiltersTest.ValidationType2 value, CancellationToken cancellationToken = default)
        {
            var errors = new List<ErrorItem>();

            if (value.IntValue < 0)
            {
                errors.Add(new ErrorItem(ValidationFiltersTest.ErrorCode));
            }

            return errors;
        }
    }
}
