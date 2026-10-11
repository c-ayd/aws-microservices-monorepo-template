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
                port: 8443,
                caBundlePath: "Certs/test-ca-bundle.crt",
                serverKeyFilePath: "Certs/test-webapi-server.key",
                serverCertificatePath: "Certs/test-webapi-server.crt");
        }

        public async Task InitializeAsync()
        {
            await WebApiFixture.InitializeAsync(
                addConfiguration: builder =>
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
