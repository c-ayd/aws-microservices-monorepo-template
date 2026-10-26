using Microsoft.Extensions.Configuration;
using Shared.Aws.Helpers.DependencyInjection;
using Shared.Aws.Helpers.Test;
using Shared.Test.Helpers.Fixtures;

namespace Shared.Test.Integration.Aws.Helpers.Collections
{
    [CollectionDefinition(nameof(AwsCollection))]
    public class AwsCollection : ICollectionFixture<AwsCollectionCluster>
    {
    }

    public class AwsCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public AwsFixture AwsFixture { get; private set; }
        public WebApiFixture WebApiFixture { get; private set; }

        public AwsCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            WebApiFixture = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                serverCertificatePath: "Certs/WebApi/test-webapi-server.crt",
                serverCertificatePrivateKeyPath: "Certs/WebApi/test-webapi-server.key");
        }

        public async Task InitializeAsync()
        {
            await AwsFixture.InitializeAsync();
            await WebApiFixture.InitializeAsync(
                overrideConfiguration: builder =>
                {
                    builder.AddInMemoryCollection([
                        new KeyValuePair<string, string?>($"AWS:ServiceURL",
                            $"http://localhost:{AwsFixture.GetPort()}"),
                        
                        new KeyValuePair<string, string?>($"AwsSes:FromEmailAddress",
                            AwsTestConstants.SesFromEmail)
                    ]);
                },
                configureServices: (services, config) =>
                {
                    services.AddAwsOptions(isProduction: false);
                    services.AddAwsSecretsManager();
                    services.AddAwsKms();
                    services.AddAwsSes();
                    services.AddAwsS3Bucket();
                },
                configureApp: null,
                configureEndpoints: null);
            
            WebApiFixture.StartServer();
        }

        public async Task DisposeAsync()
        {
            await AwsFixture.DisposeAsync();
            await WebApiFixture.DisposeAsync();
        }
    }
}
