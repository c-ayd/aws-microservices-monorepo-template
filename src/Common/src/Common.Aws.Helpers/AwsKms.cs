using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Common.Aws.Helpers.Abstractions;
using Common.Aws.Helpers.Structures;

namespace Common.Aws.Helpers
{
    internal class AwsKms : IAwsKms
    {
        private readonly IAmazonKeyManagementService _awsKms;

        public AwsKms(IAmazonKeyManagementService awsKms)
        {
            _awsKms = awsKms;
        }

        public async Task<byte[]> EncryptAsync(
            string keyIdOrAlias,
            byte[] value,
            Dictionary<string, string>? aad = null,
            CancellationToken cancellationToken = default)
        {
            await using var memoryStream = new MemoryStream(value, writable: false);
            var response = await _awsKms.EncryptAsync(new EncryptRequest()
            {
                KeyId = keyIdOrAlias,
                Plaintext = memoryStream,
                EncryptionContext = aad
            }, cancellationToken);

            return response.CiphertextBlob.ToArray();
        }
        
        public async Task<byte[]> DecryptAsync(
            byte[] encryptedValue,
            Dictionary<string, string>? aad = null,
            CancellationToken cancellationToken = default)
        {
            await using var memoryStream = new MemoryStream(encryptedValue, writable: false);
            var response = await _awsKms.DecryptAsync(new DecryptRequest()
            {
                CiphertextBlob = memoryStream,
                EncryptionContext = aad
            }, cancellationToken);

            return response.Plaintext.ToArray();
        }

        public async Task<DataKey> GenerateDataKeyAsync(
            string keyIdOrAlias,
            int numberOfBytes = 32,
            Dictionary<string, string>? aad = null,
            CancellationToken cancellationToken = default)
        {
            var response = await _awsKms.GenerateDataKeyAsync(new GenerateDataKeyRequest()
            {
                KeyId = keyIdOrAlias,
                NumberOfBytes = numberOfBytes,
                EncryptionContext = aad
            }, cancellationToken);

            return new DataKey(
                response.Plaintext.ToArray(),
                response.CiphertextBlob.ToArray()
            );
        }
    }
}
