namespace Shared.Aws.Helpers.Abstractions
{
    /// <summary>
    /// Provides methods to interact with AWS Secrets Manager.
    /// </summary>
    public interface IAwsSecretsManager
    {
        Task<string> GetSecretStringAsync(string id, bool cache = true, CancellationToken cancellationToken = default);
        Task<Dictionary<string, string>> GetMultipleSecretStringsAsync(IEnumerable<string> ids, bool cache = true, CancellationToken cancellationToken = default);
        Task<byte[]> GetSecretBinaryAsync(string id, bool cache = true, CancellationToken cancellationToken = default);
        Task<Dictionary<string, byte[]>> GetMultipleSecretBinariesAsync(IEnumerable<string> ids, bool cache = true, CancellationToken cancellationToken = default);
    }
}
