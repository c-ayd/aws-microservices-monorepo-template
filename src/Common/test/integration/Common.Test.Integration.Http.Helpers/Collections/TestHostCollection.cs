using Common.Test.Helpers.Fixtures;
using Common.Test.Integration.Http.Authentication;
using Common.Test.Integration.Http.DependencyInjection;
using Common.Test.Integration.Http.Response.Middlewares;

namespace Common.Test.Integration.Http.Collections
{
    [CollectionDefinition(nameof(TestHostCollection))]
    public class TestHostCollection : ICollectionFixture<TestHostCollectionCluster>
    {
    }

    public class TestHostCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public WebApiFixture WebApiFixture { get; private set; }

        public TestHostCollectionCluster()
        {
            WebApiFixture = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                serverCertificatePath: "Certs/WebApi/test-webapi-server.crt",
                serverCertificatePrivateKeyPath: "Certs/WebApi/test-webapi-server.key");
        }

        public async Task InitializeAsync()
        {
            await WebApiFixture.InitializeAsync(
                overrideConfiguration: null,
                configureServices: (services, configuration) =>
                {
                    ApiGatewayAuthHandlerTest.ConfigureServices(services);
                    AddValidatorTest.ConfigureServices(services);
                    ValidationFiltersTest.ConfigureServices(services);
                },
                configureApp: app =>
                {
                    AuthErrorResponseMiddlewareTest.ConfigureApp(app);
                    ApiGatewayAuthHandlerTest.ConfigureApp(app);
                },
                configureEndpoints: endpoints =>
                {
                    AuthErrorResponseMiddlewareTest.ConfigureEndpoints(endpoints);
                    ApiGatewayAuthHandlerTest.ConfigureEndpoints(endpoints);
                    ValidationFiltersTest.ConfigureEndpoints(endpoints);
                }
            );

            WebApiFixture.StartServer();
        }

        public async Task DisposeAsync()
        {
            await WebApiFixture.DisposeAsync();
        }
    }
}
