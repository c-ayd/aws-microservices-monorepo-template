namespace Shared.Aws.Helpers.Abstractions
{
    public interface ISecretsManager
    {
        Task<string> GetSecretStringAsync(string id, bool cache = true, CancellationToken cancellationToken = default);
        Task<byte[]> GetSecretBinaryAsync(string id, bool cache = true, CancellationToken cancellationToken = default);
    }
}
