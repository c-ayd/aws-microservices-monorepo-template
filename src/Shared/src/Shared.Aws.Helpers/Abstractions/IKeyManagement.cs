using Shared.Aws.Helpers.Structures;

namespace Shared.Aws.Helpers.Abstractions
{
    public interface IKeyManagement
    {
        Task<byte[]> EncryptAsync(string keyIdOrAlias, byte[] value, Dictionary<string, string>? aad = null, CancellationToken cancellationToken = default);
        Task<byte[]> DecryptAsync(byte[] valueEncrypted, Dictionary<string, string>? aad = null, CancellationToken cancellationToken = default);
        Task<EncryptionKey> GenerateEncryptionKeyAsync(string keyIdOrAlias, int numberOfBytes = 32, Dictionary<string, string>? aad = null, CancellationToken cancellationToken = default);
    }
}
