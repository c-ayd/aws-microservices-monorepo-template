using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Shared.Aws.Helpers.Test;
using Shared.Constants;
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
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public const string AwsSesTemplatePrefix = "test-template";
        public const string AwsSesTemplateSubjectLine = "Subject Line";
        public const string AwsSesTemplateBody = "Body {{BodyParam}}";
        public const string AwsSesTemplateBodyParameter = "BodyParam";

        public static int AwsPort { get; private set; }
        public static int NotificationServicePort { get; private set; }        

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
                    caBundlePath: CABundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq")
            );

            await SetupAwsAsync();

            AwsPort = AwsFixture.GetPort();
            NotificationServicePort = KestrelTestHelper.GetFreePort();

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
            public NotificationWebAppFactory()
                : base(defaultPortConfigName: "Http", CABundlePath)
            {
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpPort",
                        NotificationServicePort.ToString()),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{AwsPort}")
                ]);
            }
        }

        public async Task SetupAwsAsync()
        {
            await AwsFixture.SES_CreateTemplateAsync(
                templateName: $"{AwsSesTemplatePrefix}-{SupportedLanguages.DefaultLanguage}",
                AwsSesTemplateSubjectLine,
                AwsSesTemplateBody);

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
                        Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, CABundlePath))
                    }
                }));
        }
    }
}
