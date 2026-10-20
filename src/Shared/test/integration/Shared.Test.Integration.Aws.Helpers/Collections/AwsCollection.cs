using Microsoft.Extensions.Configuration;
using Shared.Aws.Helpers.DependencyInjection;
using Shared.Test.Helpers.Fixtures;

namespace Shared.Test.Integration.Aws.Helpers.Collections
{
    [CollectionDefinition(nameof(AwsCollection))]
    public class AwsCollection : ICollectionFixture<AwsCollectionCluster>
    {
    }

    public class AwsCollectionCluster : IAsyncLifetime
    {
        public AwsFixture AwsFixture { get; set; }
        public WebApiFixture WebApiFixture { get; set; }

        public AwsCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            WebApiFixture = new WebApiFixture(
                caBundlePath: "Certs/test-ca-bundle.crt",
                serverKeyFilePath: "Certs/WebApi/test-webapi-server.key",
                serverCertificateFilePath: "Certs/WebApi/test-webapi-server.crt");
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
                            "test@test.com")
                    ]);
                },
                configureServices: (services, config) =>
                {
                    services.AddAwsOptions(false);
                    services.AddAwsSecretsManager();
                    services.AddAwsKms();
                    services.AddAwsSes();
                    services.AddAwsS3Bucket();
                },
                configureApp: null,
                configureEndpoints: null);
            
            await AwsFixture.SesVerifyEmailAsync("test@test.com");
            WebApiFixture.StartServer();
        }

        public async Task DisposeAsync()
        {
            await AwsFixture.DisposeAsync();
            await WebApiFixture.DisposeAsync();
        }
    }
}
