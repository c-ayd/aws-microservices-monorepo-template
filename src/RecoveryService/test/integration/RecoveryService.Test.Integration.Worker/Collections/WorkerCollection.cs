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
        private const string _caBundlePath = "Certs/test-ca-bundle.crt";

        public const string ExchangeName = "test.recovery";
        public const string RoutingKey = "test.recovery.send";
        public const string QueueName = "test.recovery.queue";

        private const string _droppedMessagesDbName = "dropped-messages-db";

        private static int _awsPort;
        private static int _recoveryServicePort;

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
                    caBundlePath: _caBundlePath,
                    certificateKeyFilePath: "Certs/PostgreSql/test-postgresql-server.key",
                    certificateFilePath: "Certs/PostgreSql/test-postgresql-server.crt",
                    dbContexts: new Dictionary<string, Type>()
                    {
                        { _droppedMessagesDbName, typeof(DroppedMessagesDbContext) }
                    }),
                RabbitMqFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: _caBundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq")
            );

            await RabbitMqFixture.DeclareExchangeAsync(ExchangeName, ExchangeType.Direct);
            await RabbitMqFixture.DeclareQueueAsync(QueueName, ExchangeName, RoutingKey);

            await AwsFixture.KMS_CreateKekAsync("alias/test-alias");
            await AwsFixture.S3Bucket_CreateBucketAsync("template-graceful-shutdown");

            await AwsFixture.SecretsManager_AddStringAsync("template/recovery-svc/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        DroppedMessagesDb = PostgreSqlFixture.GetConnectionOptions(_droppedMessagesDbName),
                        RabbitMq = RabbitMqFixture.GetConnectionOptions()
                    }
                }));
            await AwsFixture.SecretsManager_AddStringAsync("template/recovery-svc/secrets", JsonSerializer.Serialize(
                new
                {
                    Storage = new
                    {
                        AwsS3BucketName = "template-graceful-shutdown",
                        Folders = new[] { "auth-svc" }
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
            _recoveryServicePort = KestrelHelper.GetFreePort();

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
            public RecoveryWebAppFactory() : base(_caBundlePath)
            {
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                base.OverrideConfiguration(configuration);

                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpPort",
                        _recoveryServicePort.ToString()),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{_awsPort}")
                ]);
            }
        }
    }
}
