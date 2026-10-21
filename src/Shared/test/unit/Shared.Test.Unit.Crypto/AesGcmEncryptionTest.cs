using System.Text;
using Shared.Crypto;
using Shared.Test.Generators;

namespace Shared.Test.Unit.Crypto
{
    public class AesGcmEncryptionTest
    {
        private readonly string _testKey = "PtdqlngVTeZD5fMzyicfhQdq8Re8H9paNdgK8M7yDc4=";

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Encrypt_WhenIsCalled_ShouldEncryptGivenValue(bool deleteKey)
        {
            // Arrange
            var value = Encoding.UTF8.GetBytes(StringGenerator.GeneratePrintableAscii());
            var key = Convert.FromBase64String(_testKey);

            // Act
            var encryptedValue = AesGcmEncryption.Encrypt(value, key, deleteKey);

            // Assert
            Assert.NotNull(encryptedValue);

            if (deleteKey)
            {
                foreach (var keyByte in key)
                {
                    Assert.Equal(0, keyByte);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Decrypt_WhenIsCalled_ShouldDecryptGivenValue(bool deleteKey)
        {
            // Arrange
            var valueStr = StringGenerator.GeneratePrintableAscii();
            var valueBytes = Encoding.UTF8.GetBytes(valueStr);
            var key = Convert.FromBase64String(_testKey);
            var encryptedValue = AesGcmEncryption.Encrypt(valueBytes, key, deleteKey: false);

            // Act
            var decryptedValue = AesGcmEncryption.Decrypt(encryptedValue, key, deleteKey);

            // Assert
            Assert.Equal(valueBytes, decryptedValue);

            if (deleteKey)
            {
                foreach (var keyByte in key)
                {
                    Assert.Equal(0, keyByte);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Compare_WhenValuesAreSame_ShouldReturnTrue(bool deleteKey)
        {
            // Arrange
            var valueStr = StringGenerator.GeneratePrintableAscii();
            var valueBytes = Encoding.UTF8.GetBytes(valueStr);
            var key = Convert.FromBase64String(_testKey);
            var encryptedValue = AesGcmEncryption.Encrypt(valueBytes, key, deleteKey: false);

            // Act
            var result = AesGcmEncryption.Compare(encryptedValue, valueBytes, key, deleteKey);

            // Assert
            Assert.True(result, "The method did not return true.");

            if (deleteKey)
            {
                foreach (var keyByte in key)
                {
                    Assert.Equal(0, keyByte);
                }
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void Compare_WhenValuesAreDifferent_ShouldReturnFalse(bool deleteKey)
        {
            // Arrange
            var valueBytes1 = Encoding.UTF8.GetBytes(StringGenerator.GeneratePrintableAscii());
            var valueBytes2 = Encoding.UTF8.GetBytes(StringGenerator.GeneratePrintableAscii());
            var key = Convert.FromBase64String(_testKey);
            var encryptedValue = AesGcmEncryption.Encrypt(valueBytes1, key, deleteKey: false);

            // Act
            var result = AesGcmEncryption.Compare(encryptedValue, valueBytes2, key, deleteKey);

            // Assert
            Assert.False(result, "The method did not return false.");

            if (deleteKey)
            {
                foreach (var keyByte in key)
                {
                    Assert.Equal(0, keyByte);
                }
            }
        }
    }
}
