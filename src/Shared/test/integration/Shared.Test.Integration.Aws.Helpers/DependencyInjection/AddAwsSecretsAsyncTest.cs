using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Shared.Aws.Helpers.DependencyInjection;
using Shared.Aws.Helpers.Test;
using Shared.Test.Generators;
using Shared.Test.Integration.Aws.Helpers.Collections;

namespace Shared.Test.Integration.Aws.Helpers.DependencyInjection
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
            var secretValue = StringGenerator.GenerateAlphanumeric();
            await _collectionCluster.AwsFixture.SecretsManager_AddStringAsync("aws-secrets-manager/di-1", JsonSerializer.Serialize(new { Key = secretValue }));

            var configuration = new ConfigurationManager();
            configuration.AddInMemoryCollection([
                new KeyValuePair<string, string?>("AWS:ServiceURL",
                    $"http://localhost:{_collectionCluster.AwsFixture.GetPort()}"),
                new KeyValuePair<string, string?>("AWS:Region",
                    AwsTestConstants.Region)
            ]);

            // Act
            await configuration.AddAwsSecretsAsync([
                "aws-secrets-manager/di-1"
            ]);

            // Assert
            Assert.Equal(secretValue, configuration.GetValue<string>("Key"));
        }
    }
}
