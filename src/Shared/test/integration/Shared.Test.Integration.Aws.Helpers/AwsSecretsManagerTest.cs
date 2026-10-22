using System.Text;
using Amazon.SecretsManager.Model;
using Shared.Aws.Helpers;
using Shared.Aws.Helpers.Abstractions;
using Shared.Test.Generators;
using Shared.Test.Integration.Aws.Helpers.Collections;

namespace Shared.Test.Integration.Aws.Helpers
{
    [Collection(nameof(AwsCollection))]
    public class AwsSecretsManagerTest
    {
        private readonly AwsCollectionCluster _collectionCluster;
        
        public AwsSecretsManagerTest(AwsCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task GetSecretStringAsync_WhenMethodIsCalled_ShouldReturnValue()
        {
            // Arrange
            var id = "aws-secrets-manager/test-str";
            var value = StringGenerator.GenerateAlphanumeric();
            await _collectionCluster.AwsFixture.SecretsManager_AddStringAsync(id, value);

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();

            // Act
            var result = await awsSecretsManager.GetSecretStringAsync(id);

            // Assert
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task GetSecretBinaryAsync_WhenMethodIsCalled_ShouldReturnValue()
        {
            // Arrange
            var id = "aws-secrets-manager/test-binary";
            var value = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.SecretsManager_AddBinaryAsync(id, value);

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();

            // Act
            var result = await awsSecretsManager.GetSecretBinaryAsync(id);

            // Assert
            Assert.Equal(value, result);
        }
    }
}
