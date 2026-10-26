using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Shared.Aws.Helpers.Test;
using Shared.Test.Helpers;
using Shared.Test.Helpers.Fixtures;

namespace NotificationService.Test.Integration.Worker.Collections
{
    [CollectionDefinition(nameof(WorkerCollection))]
    public class WorkerCollection : ICollectionFixture<WorkerCollectionCluster>
    {
    }

    public class WorkerCollectionCluster : IAsyncLifetime
    {
        private const string _caBundlePath = "Certs/test-ca-bundle.crt";

        private static int _awsPort;
        private static int _notificationServicePort;

        public AwsFixture AwsFixture { get; private set; }
        public RabbitMqFixture RabbitMqFixture { get; private set; }

        public NotificationWebAppFactory NotificationWebApp { get; private set; } = null!;

        public WorkerCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            RabbitMqFixture = new RabbitMqFixture();
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                AwsFixture.InitializeAsync(),
                RabbitMqFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: _caBundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq")
            );

            await AwsFixture.SES_CreateTemplateAsync("test-template-en", "Subject Line", "Body {{Body}}");

            await AwsFixture.SecretsManager_AddStringAsync("template/notification-svc/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        RabbitMq = RabbitMqFixture.GetConnectionOptions()
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
                new
                {
                   CABundle = new
                   {
                       Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _caBundlePath))
                   }
                }));

            _awsPort = AwsFixture.GetPort();
            _notificationServicePort = KestrelHelper.GetFreePort();

            NotificationWebApp = new NotificationWebAppFactory();
            NotificationWebApp.StartServer();
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                AwsFixture.DisposeAsync(),
                RabbitMqFixture.DisposeAsync()
            );
            
            await NotificationWebApp.DisposeAsync();
        }

        public class NotificationWebAppFactory : WebAppFactoryFixture<Program>
        {
            public NotificationWebAppFactory() : base(_caBundlePath)
            {
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpPort",
                        _notificationServicePort.ToString()),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{_awsPort}")
                ]);
            }
        }
    }
}
