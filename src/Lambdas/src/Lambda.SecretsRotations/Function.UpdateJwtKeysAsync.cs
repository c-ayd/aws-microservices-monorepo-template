using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.SecretsManager.Model;
using Common.Aws.Helpers.Options;
using Lambda.SecretsRotations.Exceptions;
using Lambda.SecretsRotations.Utilities;

namespace Lambda.SecretsRotations
{
    public partial class Function
    {
        private async Task UpdateJwtKeysAsync(
            BatchGetSecretValueResponse secrets,
            string jwtKeysSecretId,
            DateTimeOffset startDate,
            ILambdaLogger logger)
        {
            // Generate public and private key
            Utility.GenerateRsaKeys(out var publicKey, out var privateKey);

            // Create new JWT keys options
            var keyId = startDate.ToString("yyyy-MM");
            var jwtKeysOptions = new JwtKeysOptionsBase()
            {
                CurrentKeyId = keyId,
                Keys = new List<JwtKeysOptionsBase.KeyInfo>()
                {
                    new JwtKeysOptionsBase.KeyInfo()
                    {
                        KeyId = keyId,
                        PublicKey = publicKey,
                        PrivateKey = privateKey,
                        NotBeforeTime = startDate.ToUnixTimeSeconds(),
                        // In case something goes wrong while rotating the JWT keys, extra 7 days are given
                        // to the expiration date, so that the authentication can continue and there will be
                        // enough time to fix the issue.
                        ExpirationTime = startDate.AddMonths(3).AddDays(7).ToUnixTimeSeconds()
                    }
                }
            };

            // Deserialize the old JWT keys secret to find and keep the latest key in the new secret value
            Common.Lambda.Helpers.Utilities.Utility.TryDeserializeJson<JwtKeysOptionsBase>(
                json: secrets.SecretValues
                .Where(s => s.Name == jwtKeysSecretId)
                .Single()
                .SecretString,
                section: JwtKeysOptionsBase.Key,
                out var oldJwtKeys);

            var currentKey = oldJwtKeys!.Keys.MaxBy(k => k.NotBeforeTime);
            if (currentKey == null)
                throw new JwtKeyMissingException();

            jwtKeysOptions.Keys.Add(currentKey);

            // Update the secret
            await _awsSecretsManager.PutSecretValueAsync(new PutSecretValueRequest()
            {
                SecretId = jwtKeysSecretId,
                SecretString = JsonSerializer.Serialize(new Dictionary<string, JwtKeysOptionsBase>()
                {
                    { JwtKeysOptionsBase.Key, jwtKeysOptions }
                })
            });

            logger.LogInformation("JWT - The keys have been updated.");
        }
    }
}
