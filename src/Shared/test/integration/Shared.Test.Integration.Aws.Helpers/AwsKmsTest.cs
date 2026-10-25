using System.Text;
using Shared.Aws.Helpers.Abstractions;
using Shared.Test.Generators;
using Shared.Test.Integration.Aws.Helpers.Collections;

namespace Shared.Test.Integration.Aws.Helpers
{
    [Collection(nameof(AwsCollection))]
    public class AwsKmsTest
    {
        private readonly AwsCollectionCluster _collectionCluster;
        
        public AwsKmsTest(AwsCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task EncryptAsync_WhenMethodIsCalled_ShouldEncryptValue()
        {
            // Arrange
            var alias = "alias/aws-kms/encrypt";
            await _collectionCluster.AwsFixture.KMS_CreateKekAsync(alias);

            var awsKms = _collectionCluster.WebApiFixture.GetService<IAwsKms>();

            var value = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());

            // Act
            var encryptedValue = await awsKms.EncryptAsync(alias, value);

            // Assert
            Assert.NotNull(encryptedValue);
        }

        [Fact]
        public async Task DecryptAsync_WhenEncryptedValueIsInvalid_ShouldThrowException()
        {
            // Arrange
            var awsKms = _collectionCluster.WebApiFixture.GetService<IAwsKms>();

            // Act
            var exception = Record.ExceptionAsync(async () =>
            {
                await awsKms.DecryptAsync([0, 1, 2, 3, 4, 5]);
            });

            // Assert
            Assert.NotNull(exception);
        }

        [Fact]
        public async Task DecryptAsync_WhenEncryptedValueIsValid_ShouldDecryptValue()
        {
            // Arrange
            var alias = "alias/aws-kms/decrypt";
            await _collectionCluster.AwsFixture.KMS_CreateKekAsync(alias);

            var awsKms = _collectionCluster.WebApiFixture.GetService<IAwsKms>();

            var value = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            var encryptedValue = await awsKms.EncryptAsync(alias, value);

            // Act
            var decryptedValue = await awsKms.DecryptAsync(encryptedValue);

            // Assert
            Assert.Equal(value, decryptedValue);
        }

        [Fact]
        public async Task GenerateEncryptionKeyAsync_WhenMethodIsCalled_ShouldReturnEncryptionKeys()
        {
            // Arrange
            var alias = "alias/aws-kms/encryption-key";
            await _collectionCluster.AwsFixture.KMS_CreateKekAsync(alias);

            var awsKms = _collectionCluster.WebApiFixture.GetService<IAwsKms>();

            // Act
            var dataKey = await awsKms.GenerateDataKeyAsync(alias);
            var dataKeyPlaintext = await awsKms.DecryptAsync(dataKey.Encrypted);

            // Assert
            Assert.Equal(dataKey.Plaintext, dataKeyPlaintext);
            Assert.NotNull(dataKey.Encrypted);
        }
    }
}
