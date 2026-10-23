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

        public async Task<Dictionary<string, string>> GetMultipleSecretStringsAsync(
            IEnumerable<string> ids,
            bool cache = true,
            CancellationToken cancellationToken = default)
        {
            var secrets = new Dictionary<string, string>();
            var idsToFetch = new List<string>();
            foreach (var id in ids)
            {
                if (_cacheString.TryGetValue(id, out var value))
                {
                    secrets.Add(id, value);
                }
                else
                {
                    idsToFetch.Add(id);
                }
            }

            if (idsToFetch.Count == 0)
                return secrets;

            var response = await _awsSecretsManager.BatchGetSecretValueAsync(new BatchGetSecretValueRequest()
            {
                SecretIdList = idsToFetch
            }, cancellationToken);

            foreach (var secretValue in response.SecretValues)
            {
                secrets.Add(secretValue.Name, secretValue.SecretString);
                if (cache)
                {
                    _cacheString.AddOrUpdate(secretValue.Name, secretValue.SecretString, (key, oldValue) => secretValue.SecretString);
                }
            }

            return secrets;
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

        public async Task<Dictionary<string, byte[]>> GetMultipleSecretBinariesAsync(
            IEnumerable<string> ids,
            bool cache = true,
            CancellationToken cancellationToken = default)
        {
            var secrets = new Dictionary<string, byte[]>();
            var idsToFetch = new List<string>();
            foreach (var id in ids)
            {
                if (_cacheBinary.TryGetValue(id, out var value))
                {
                    secrets.Add(id, value);
                }
                else
                {
                    idsToFetch.Add(id);
                }
            }

            if (idsToFetch.Count == 0)
                return secrets;

            var response = await _awsSecretsManager.BatchGetSecretValueAsync(new BatchGetSecretValueRequest()
            {
                SecretIdList = idsToFetch
            }, cancellationToken);

            foreach (var secretValue in response.SecretValues)
            {
                var value = secretValue.SecretBinary.ToArray();
                secrets.Add(secretValue.Name, value);
                if (cache)
                {
                    _cacheBinary.AddOrUpdate(secretValue.Name, value, (key, oldValue) => value);
                }
            }

            return secrets;
        }
    }
}
