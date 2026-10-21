using Shared.Test.Helpers.Fixtures;
using Shared.Test.Integration.Http.Authentication;
using Shared.Test.Integration.Http.DependencyInjection;
using Shared.Test.Integration.Http.Response.Middlewares;

namespace Shared.Test.Integration.Http.Collections
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
                serverKeyFilePath: "Certs/WebApi/test-webapi-server.key",
                serverCertificateFilePath: "Certs/WebApi/test-webapi-server.crt");
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
