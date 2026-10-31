using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Common.Aws.Helpers.DependencyInjection;
using Common.Aws.Helpers.Test;
using Common.Test.Generators;
using Common.Test.Integration.Aws.Helpers.Collections;

namespace Common.Test.Integration.Aws.Helpers.DependencyInjection
{
    [Collection(nameof(AwsCollection))]
    public class AddAwsSecretsAsyncTest
    {
        private readonly AwsCollectionCluster _collectionCluster;

        public AddAwsSecretsAsyncTest(AwsCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task AddAwsSecretsAsync_WhenSecretsExist_ShouldReturnAll()
        {
            // Arrange
            var secretId = "aws-secrets-manager/di-1";
            var secretValue = StringGenerator.GenerateAlphanumeric();
            await _collectionCluster.AwsFixture.SecretsManager_AddStringAsync(
                secretId,
                value: JsonSerializer.Serialize(new { Key = secretValue }));

            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>("AWS:ServiceURL",
                    $"http://localhost:{_collectionCluster.AwsFixture.GetPort()}"),
                new KeyValuePair<string, string?>("AWS:Region",
                    AwsTestConstants.Region)
            ]);

            // Act
            await configuration.AddAwsSecretsAsync([secretId]);

            // Assert
            Assert.Equal(secretValue, configuration.GetValue<string>("Key"));
        }
    }
}
