using System.Text.Json;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Microsoft.Extensions.Configuration;
using Common.Aws.Helpers.Test;

namespace Common.Aws.Helpers.DependencyInjection
{
    public static partial class DependencyInjection
    {
        public static async Task AddAwsSecretsAsync(this IConfigurationManager configuration, List<string> ids)
        {
            using var awsSecretsManagerClient = AwsConfig.IsProduction ?
                new AmazonSecretsManagerClient() :
                new AmazonSecretsManagerClient(AwsTestConstants.Credentials, new AmazonSecretsManagerConfig()
                {
                    ServiceURL = configuration["AWS:ServiceURL"],
                    AuthenticationRegion = AwsTestConstants.Region
                });

            var response = await awsSecretsManagerClient.BatchGetSecretValueAsync(new BatchGetSecretValueRequest()
            {
                SecretIdList = ids
            });

            var configValues = new Dictionary<string, string?>();
            foreach (var secret in response.SecretValues)
            {
                using var jsonDoc = JsonDocument.Parse(secret.SecretString);
                FlattenJson(jsonDoc.RootElement, string.Empty, configValues);
            }

            configuration.AddInMemoryCollection(configValues);
        }

        private static void FlattenJson(JsonElement element, string prefix, Dictionary<string, string?> configValues)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}:{prop.Name}";
                        FlattenJson(prop.Value, key, configValues);
                    }
                    break;
                case JsonValueKind.Array:
                    int index = 0;
                    foreach (var item in element.EnumerateArray())
                    {
                        var arrayKey = $"{prefix}:{index++}";
                        FlattenJson(item, arrayKey, configValues);
                    }
                    break;
                case JsonValueKind.String:
                    configValues[prefix] = element.GetString();
                    break;
                case JsonValueKind.Null:
                    configValues[prefix] = null;
                    break;
                case JsonValueKind.Number:
                case JsonValueKind.True:
                case JsonValueKind.False:
                    configValues[prefix] = element.GetRawText();
                    break;
                default:
                    break;
            }
        }
    }
}
