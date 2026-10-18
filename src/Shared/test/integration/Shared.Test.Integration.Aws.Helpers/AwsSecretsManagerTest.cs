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
        public async Task GetSecretStringAsync_WhenIdDoesNotExist_ShouldThrowException()
        {
            // Arrange
            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();

            // Act
            var exception = Record.ExceptionAsync(async () =>
            {
                await awsSecretsManager.GetSecretStringAsync(StringGenerator.GenerateAlphanumeric());
            });

            // Assert
            Assert.NotNull(exception);
        }

        [Fact]
        public async Task GetSecretStringAsync_WhenIdExists_ShouldReturnValue()
        {
            // Arrange
            var id = "aws-secrets-manager/test-str";
            var value = "Test value";
            await _collectionCluster.AwsFixture.AddSecretStringAsync(id, value);

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();

            // Act
            var result = await awsSecretsManager.GetSecretStringAsync(id);

            // Assert
            Assert.Equal(value, result);
        }

        [Fact]
        public async Task GetSecretBinaryAsync_WhenIdDoesNotExist_ShouldThrowException()
        {
            // Arrange
            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();

            // Act
            var exception = Record.ExceptionAsync(async () =>
            {
                await awsSecretsManager.GetSecretBinaryAsync(StringGenerator.GenerateAlphanumeric());
            });

            // Assert
            Assert.NotNull(exception);
        }

        [Fact]
        public async Task GetSecretBinaryAsync_WhenIdExists_ShouldReturnValue()
        {
            // Arrange
            var id = "aws-secrets-manager/test-binary";
            var value = Encoding.UTF8.GetBytes("Test value");
            await _collectionCluster.AwsFixture.AddSecretBinaryAsync(id, value);

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();

            // Act
            var result = await awsSecretsManager.GetSecretBinaryAsync(id);

            // Assert
            Assert.Equal(value, result);
        }
    }
}
