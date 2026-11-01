using System.Text.Json;
using AdminService.Test.Integration.Application.Collections.GrpcServices;
using Common.Grpc.Helpers.DependencyInjection;
using Common.Test.Helpers;
using Common.Test.Helpers.Fixtures;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;

namespace AdminService.Test.Integration.Application.Collections
{
    [CollectionDefinition(nameof(AdminApiCollection))]
    public class AdminApiCollection : ICollectionFixture<AdminApiCollectionCluster>
    {
    }

    public class AdminApiCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        private const string _adminServiceServerCertificatePath = "Certs/test-admin-service-server.crt";
        private const string _adminServiceServerCertificatePrivateKeyPath = "Certs/test-admin-service-server.key";
        private const string _webApiServerCertificatePath = "Certs/WebApi/test-webapi-server.crt";
        private const string _webApiServerPrivateKeyPath = "Certs/WebApi/test-webapi-server.key";

        public static int AwsPort { get; private set; }
        public static int GrpcWebAppPort { get; private set; }
        public static int AdminApiHttpsPort { get; private set; }

        public AwsFixture AwsFixture { get; private set; }
        public WebApiFixture GrpcWebApp { get; private set; }
        
        public AdminApiWebAppFactory AdminApiWebApp { get; private set; } = null!;

        public AdminApiCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            GrpcWebApp = new WebApiFixture(
                sslEnabled: true,
                caBundlePath: CABundlePath,
                serverCertificatePath: _webApiServerCertificatePath,
                serverCertificatePrivateKeyPath: _webApiServerPrivateKeyPath);
            
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                AwsFixture.InitializeAsync(),
                GrpcWebApp.InitializeAsync(
                    overrideConfiguration: null,
                    configureServices: (services, configuration) =>
                    {
                        services.AddGrpcServer(false);
                    },
                    configureApp: null,
                    configureEndpoints: endpoints =>
                    {
                        endpoints.MapGrpcService<AuthGrpcAdminTestService>();
                    })
            );

            GrpcWebApp.StartServer();

            await SetupAws();

            AwsPort = AwsFixture.GetPort();
            GrpcWebAppPort = GrpcWebApp.GetPort(WebApiFixture.HttpsPortConfigName);
            AdminApiHttpsPort = KestrelTestHelper.GetAvailablePortNumber();

            AdminApiWebApp = new AdminApiWebAppFactory();
            AdminApiWebApp.StartServer();
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                AwsFixture.DisposeAsync(),
                GrpcWebApp.DisposeWebApiAsync()
            );
            
            await AdminApiWebApp.DisposeAsync();
        }

        public class AdminApiWebAppFactory : WebAppFactoryFixture<Program>
        {
            public AdminApiWebAppFactory()
                : base(defaultPortConfigName: "HttpsOnly", CABundlePath)
            {
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpsPort",
                        AdminApiHttpsPort.ToString()),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{AwsPort}"),

                    new KeyValuePair<string, string?>("Grpc:Auth",
                        $"https://localhost:{GrpcWebAppPort}")
                ]);
            }
        }

        private async Task SetupAws()
        {
            await AwsFixture.SecretsManager_AddStringAsync("template/admin-svc/certificates", JsonSerializer.Serialize(
                new
                {
                    Certificates = new
                    {
                        ServerCertificate = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _adminServiceServerCertificatePath)),
                        ServerPrivateKey = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _adminServiceServerCertificatePrivateKeyPath))
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
                new
                {
                    CABundle = new
                    {
                        Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, CABundlePath))
                    }
                }));
        }
    }
}
