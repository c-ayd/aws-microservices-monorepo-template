using Shared.Aws.Helpers.Structures;

namespace Shared.Aws.Helpers.Abstractions
{
    /// <summary>
    /// Provides methods to interact with AWS KMS.
    /// </summary>
    public interface IAwsKms
    {
        Task<byte[]> EncryptAsync(string keyIdOrAlias, byte[] value, Dictionary<string, string>? aad = null, CancellationToken cancellationToken = default);
        Task<byte[]> DecryptAsync(byte[] encryptedValue, Dictionary<string, string>? aad = null, CancellationToken cancellationToken = default);
        Task<DataKey> GenerateDataKeyAsync(string keyIdOrAlias, int numberOfBytes = 32, Dictionary<string, string>? aad = null, CancellationToken cancellationToken = default);
    }
}
