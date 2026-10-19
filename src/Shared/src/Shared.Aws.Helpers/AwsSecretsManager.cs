using System.Collections.Concurrent;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Shared.Aws.Helpers.Abstractions;

namespace Shared.Aws.Helpers
{
    internal class AwsSecretsManager : ISecretsManager
    {
        private readonly ConcurrentDictionary<string, string> _cacheString = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, byte[]> _cacheBinary = new ConcurrentDictionary<string, byte[]>();

        private readonly IAmazonSecretsManager _awsSecretsManager;
        
        public AwsSecretsManager(IAmazonSecretsManager awsSecretsManager)
        {
            _awsSecretsManager = awsSecretsManager;
        }

        public async Task<string> GetSecretStringAsync(
            string id,
            bool cache = true,
            CancellationToken cancellationToken = default)
        {
            if (_cacheString.TryGetValue(id, out var value))
                return value;

            var response = await _awsSecretsManager.GetSecretValueAsync(new GetSecretValueRequest()
            {
                SecretId = id
            }, cancellationToken);

            if (cache)
            {
                _cacheString.AddOrUpdate(id, response.SecretString, (key, oldValue) => response.SecretString);
            }

            return response.SecretString;
        }

        public async Task<byte[]> GetSecretBinaryAsync(
            string id,
            bool cache = true,
            CancellationToken cancellationToken = default)
        {
            if (_cacheBinary.TryGetValue(id, out var value))
                return value;

            var response = await _awsSecretsManager.GetSecretValueAsync(new GetSecretValueRequest()
            {
                SecretId = id
            }, cancellationToken);

            value = response.SecretBinary.ToArray();
            if (cache)
            {
                _cacheBinary.AddOrUpdate(id, value, (key, oldValue) => value);
            }

            return value;
        }
    }
}
