using System.Text.Json;
using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;
using Shared.Test.Helpers;
using Shared.Test.Helpers.Fixtures;

namespace RecoveryService.Test.Integration.Worker.Collections
{
    [CollectionDefinition(nameof(WorkerCollection))]
    public class WorkerCollection : ICollectionFixture<WorkerCollectionCluster>
    {
    }

    public class WorkerCollectionCluster : IAsyncLifetime
    {
        public const string CABundlePath = "Certs/test-ca-bundle.crt";

        public const string AwsKmsAliasForDroppedMessages = "alias/dropped-messages";
        public const string AwsS3BucketNameForDroppedMessages = "template-dropped-messages";

        public const string DroppedMessagesDbName = "dropped-messages-db";

        public const string TestServiceName = "TestService";
        public const string TestPublisherName = "TestPublisher";
        public const string TestExchangeName = "test.recovery";
        public const string TestRoutingKey = "test.recovery.send";
        public const string TestQueueName = "test.recovery.queue";

        public static int AwsPort { get; private set; }
        public static int RecoveryServicePort { get; private set; }

        public AwsFixture AwsFixture { get; private set; }
        public PostgreSqlFixture PostgreSqlFixture { get; private set; }
        public RabbitMqFixture RabbitMqFixture { get; private set; }

        public RecoveryWebAppFactory RecoveryWebApp { get; private set; } = null!;

        public WorkerCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            PostgreSqlFixture = new PostgreSqlFixture();
            RabbitMqFixture = new RabbitMqFixture();
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                AwsFixture.InitializeAsync(),
                PostgreSqlFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    serverCertificatePath: "Certs/PostgreSql/test-postgresql-server.crt",
                    serverCertificatePrivateKeyPath: "Certs/PostgreSql/test-postgresql-server.key",
                    dbContexts: new Dictionary<string, Type>()
                    {
                        { DroppedMessagesDbName, typeof(DroppedMessagesDbContext) }
                    }),
                RabbitMqFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq")
            );

            await SetupAwsAsync();
            await SetupRabbitMqAsync();

            AwsPort = AwsFixture.GetPort();
            RecoveryServicePort = KestrelHelper.GetFreePort();

            RecoveryWebApp = new RecoveryWebAppFactory();
            RecoveryWebApp.StartServer();
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                AwsFixture.DisposeAsync(),
                PostgreSqlFixture.DisposeAsync(),
                RabbitMqFixture.DisposeAsync()
            );

            await RecoveryWebApp.DisposeAsync();
        }

        public class RecoveryWebAppFactory : WebAppFactoryFixture<Program>
        {
            public RecoveryWebAppFactory() : base(CABundlePath)
            {
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                base.OverrideConfiguration(configuration);

                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpPort",
                        RecoveryServicePort.ToString()),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{AwsPort}")
                ]);
            }
        }

        private async Task SetupAwsAsync()
        {
            await AwsFixture.KMS_CreateKekAsync(AwsKmsAliasForDroppedMessages);
            await AwsFixture.S3Bucket_CreateBucketAsync(AwsS3BucketNameForDroppedMessages);

            await AwsFixture.SecretsManager_AddStringAsync("template/recovery-svc/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        DroppedMessagesDb = PostgreSqlFixture.GetConnectionOptions(DroppedMessagesDbName),
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

        private async Task SetupRabbitMqAsync()
        {
            await RabbitMqFixture.DeclareExchangeAsync(TestExchangeName, ExchangeType.Direct);
            await RabbitMqFixture.DeclareQueueAsync(TestQueueName, TestExchangeName, TestRoutingKey);
        }
    }
}
