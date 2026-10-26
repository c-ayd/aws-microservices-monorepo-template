using Shared.Test.Helpers.Fixtures;
using Shared.Test.Integration.AspNetCore.Helpers.Options;

namespace Shared.Test.Integration.AspNetCore.Helpers.Collections
{
    [CollectionDefinition(nameof(TestHostCollection))]
    public class TestHostCollection : ICollectionFixture<TestHostCollectionCluster>
    {
    }

    public class TestHostCollectionCluster : IAsyncLifetime
    {
        public WebApiFixture WebApiFixture { get; private set; }

        public TestHostCollectionCluster()
        {
            WebApiFixture = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: "Certs/test-ca-bundle.crt",
                serverCertificatePath: "Certs/WebApi/test-webapi-server.crt",
                serverCertificatePrivateKeyPath: "Certs/WebApi/test-webapi-server.key");
        }

        public async Task InitializeAsync()
        {
            await WebApiFixture.InitializeAsync(
                overrideConfiguration: builder =>
                {
                    AddOptionsTest.AddConfiguration(builder);
                },
                configureServices: (services, configuration) =>
                {
                    AddOptionsTest.ConfigureServices(services, configuration);
                },
                configureApp: null,
                configureEndpoints: null
            );
        }

        public async Task DisposeAsync()
        {
            await WebApiFixture.DisposeAsync();
        }
    }
}
