using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Initializer.Databases.Options;
using Microsoft.EntityFrameworkCore;
using Common.Aws.Helpers.Test;
using Initializer.Databases.Rollouts;

namespace Initializer.Databases
{
    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            System.Console.WriteLine("Auth DB migration(s) started.");

            using var awsSecretsManagerClient = !args.Contains("--test") ?
                new AmazonSecretsManagerClient() :
                new AmazonSecretsManagerClient(AwsTestConstants.Credentials, new AmazonSecretsManagerConfig()
                {
                    ServiceURL = Utility.GetArgsValue(args, ["--aws-url"]),
                    AuthenticationRegion = AwsTestConstants.Region
                });

            var secretIds = new List<string>()
            {
                "template/authdb-job/connections",
                "template/authdb-job/seed-data",
                "template/cert/ca-bundle"
            };

            var response = await awsSecretsManagerClient.BatchGetSecretValueAsync(new BatchGetSecretValueRequest()
            {
                SecretIdList = secretIds
            });

            var connectionsOptions = Utility.DeserializeOptions<ConnectionsOptions>(response.SecretValues.FirstOrDefault(s => s.Name == "template/authdb-job/connections")!.SecretString)!;
            var seedDataOptions = Utility.DeserializeOptions<SeedDataOptions>(response.SecretValues.FirstOrDefault(s => s.Name == "template/authdb-job/seed-data")!.SecretString)!;
            var caBundleOptions = Utility.DeserializeOptions<CABundleOptions>(response.SecretValues.FirstOrDefault(s => s.Name == "template/cert/ca-bundle")!.SecretString)!;

            await AuthDb.RolloutAsync(connectionsOptions.AuthDb, caBundleOptions, seedDataOptions.AuthDbSeedData);
            await DroppedMessagesDb.RolloutAsync(connectionsOptions.DroppedMessagesDb, caBundleOptions);
            
            return 0;
        }
    }
}
