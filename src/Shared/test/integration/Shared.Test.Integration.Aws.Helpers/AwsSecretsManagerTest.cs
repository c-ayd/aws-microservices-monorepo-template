using System.Text;
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
        public async Task GetMultipleSecretStringsAsync_WhenThereIsNoCachedSecretAndMethodIsCalled_ShouldReturnAllSecretsFromAws()
        {
            // Arrange
            var secrets = new Dictionary<string, string>()
            {
                { "aws-secret-manager/multiple-1", StringGenerator.GenerateAlphanumeric() },
                { "aws-secret-manager/multiple-2", StringGenerator.GenerateAlphanumeric() },
                { "aws-secret-manager/multiple-3", StringGenerator.GenerateAlphanumeric() }
            };

            foreach (var secret in secrets)
            {
                await _collectionCluster.AwsFixture.SecretsManager_AddStringAsync(secret.Key, secret.Value);
            }

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();

            // Act
            var result = await awsSecretsManager.GetMultipleSecretStringsAsync(secrets.Keys);

            // Assert
            Assert.Equal(secrets.Count, result.Count);
            foreach (var secret in secrets)
            {
                if (!result.TryGetValue(secret.Key, out var value))
                    Assert.Fail($"The returned secrets do not contain the secret {secret.Key}");

                Assert.Equal(secret.Value, value);
            }
        }

        [Fact]
        public async Task GetMultipleSecretStringsAsync_WhenSomeSecretsAreCachedAndMethodIsCalled_ShouldReturnMissingSecretsFromAws()
        {
            // Arrange
            var secrets = new Dictionary<string, string>()
            {
                { "aws-secret-manager/multiple-4", StringGenerator.GenerateAlphanumeric() },
                { "aws-secret-manager/multiple-5", StringGenerator.GenerateAlphanumeric() },
                { "aws-secret-manager/multiple-6", StringGenerator.GenerateAlphanumeric() }
            };

            foreach (var secret in secrets)
            {
                await _collectionCluster.AwsFixture.SecretsManager_AddStringAsync(secret.Key, secret.Value);
            }

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();
            await awsSecretsManager.GetSecretStringAsync(secrets.Keys.ElementAt(0));

            // Act
            var result = await awsSecretsManager.GetMultipleSecretStringsAsync(secrets.Keys);

            // Assert
            Assert.Equal(secrets.Count, result.Count);
            foreach (var secret in secrets)
            {
                if (!result.TryGetValue(secret.Key, out var value))
                    Assert.Fail($"The returned secrets do not contain the secret {secret.Key}");

                Assert.Equal(secret.Value, value);
            }
        }

        [Fact]
        public async Task GetMultipleSecretStringsAsync_WhenAllSecretsAreCachedAndMethodIsCalled_ShouldReturnAllSecretsFromCache()
        {
            // Arrange
            var secrets = new Dictionary<string, string>()
            {
                { "aws-secret-manager/multiple-7", StringGenerator.GenerateAlphanumeric() },
                { "aws-secret-manager/multiple-8", StringGenerator.GenerateAlphanumeric() },
                { "aws-secret-manager/multiple-9", StringGenerator.GenerateAlphanumeric() }
            };

            foreach (var secret in secrets)
            {
                await _collectionCluster.AwsFixture.SecretsManager_AddStringAsync(secret.Key, secret.Value);
            }

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();
            foreach (var secret in secrets)
            {
                await awsSecretsManager.GetSecretStringAsync(secret.Key);
            }

            await _collectionCluster.AwsFixture.BlockTrafficAsync();

            // Act
            var result = await awsSecretsManager.GetMultipleSecretStringsAsync(secrets.Keys);

            // Assert
            await _collectionCluster.AwsFixture.UnblockTrafficAsync();

            Assert.Equal(secrets.Count, result.Count);
            foreach (var secret in secrets)
            {
                if (!result.TryGetValue(secret.Key, out var value))
                    Assert.Fail($"The returned secrets do not contain the secret {secret.Key}");

                Assert.Equal(secret.Value, value);
            }
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

        [Fact]
        public async Task GetMultipleSecretBinariesAsync_WhenThereIsNoCachedSecretAndMethodIsCalled_ShouldReturnAllSecretsFromAws()
        {
            // Arrange
            var secrets = new Dictionary<string, byte[]>()
            {
                { "aws-secret-manager/multiple-10", [1, 2, 3, 4, 5] },
                { "aws-secret-manager/multiple-11", [6, 7, 8, 9, 10] },
                { "aws-secret-manager/multiple-12", [11, 12, 13, 14, 15] }
            };

            foreach (var secret in secrets)
            {
                await _collectionCluster.AwsFixture.SecretsManager_AddBinaryAsync(secret.Key, secret.Value);
            }

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();

            // Act
            var result = await awsSecretsManager.GetMultipleSecretBinariesAsync(secrets.Keys);

            // Assert
            Assert.Equal(secrets.Count, result.Count);
            foreach (var secret in secrets)
            {
                if (!result.TryGetValue(secret.Key, out var value))
                    Assert.Fail($"The returned secrets do not contain the secret {secret.Key}");

                Assert.Equal(secret.Value, value);
            }
        }

        [Fact]
        public async Task GetMultipleSecretBinariesAsync_WhenSomeSecretsAreCachedAndMethodIsCalled_ShouldReturnMissingSecretsFromAws()
        {
            // Arrange
            var secrets = new Dictionary<string, byte[]>()
            {
                { "aws-secret-manager/multiple-13", [1, 2, 3, 4, 5] },
                { "aws-secret-manager/multiple-14", [6, 7, 8, 9, 10] },
                { "aws-secret-manager/multiple-15", [11, 12, 13, 14, 15] }
            };

            foreach (var secret in secrets)
            {
                await _collectionCluster.AwsFixture.SecretsManager_AddBinaryAsync(secret.Key, secret.Value);
            }

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();
            await awsSecretsManager.GetSecretBinaryAsync(secrets.Keys.ElementAt(0));

            // Act
            var result = await awsSecretsManager.GetMultipleSecretBinariesAsync(secrets.Keys);

            // Assert
            Assert.Equal(secrets.Count, result.Count);
            foreach (var secret in secrets)
            {
                if (!result.TryGetValue(secret.Key, out var value))
                    Assert.Fail($"The returned secrets do not contain the secret {secret.Key}");

                Assert.Equal(secret.Value, value);
            }
        }

        [Fact]
        public async Task GetMultipleSecretBinariesAsync_WhenAllSecretsAreCachedAndMethodIsCalled_ShouldReturnAllSecretsFromCache()
        {
            // Arrange
            var secrets = new Dictionary<string, byte[]>()
            {
                { "aws-secret-manager/multiple-16", [1, 2, 3, 4, 5] },
                { "aws-secret-manager/multiple-17", [6, 7, 8, 9, 10] },
                { "aws-secret-manager/multiple-18", [11, 12, 13, 14, 15] }
            };

            foreach (var secret in secrets)
            {
                await _collectionCluster.AwsFixture.SecretsManager_AddBinaryAsync(secret.Key, secret.Value);
            }

            var awsSecretsManager = _collectionCluster.WebApiFixture.GetService<ISecretsManager>();
            foreach (var secret in secrets)
            {
                await awsSecretsManager.GetSecretBinaryAsync(secret.Key);
            }

            await _collectionCluster.AwsFixture.BlockTrafficAsync();

            // Act
            var result = await awsSecretsManager.GetMultipleSecretBinariesAsync(secrets.Keys);

            // Assert
            await _collectionCluster.AwsFixture.UnblockTrafficAsync();

            Assert.Equal(secrets.Count, result.Count);
            foreach (var secret in secrets)
            {
                if (!result.TryGetValue(secret.Key, out var value))
                    Assert.Fail($"The returned secrets do not contain the secret {secret.Key}");

                Assert.Equal(secret.Value, value);
            }
        }
    }
}
