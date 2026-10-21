using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NotificationService.Worker.DbContexts;
using NotificationService.Worker.Options;
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

        private const string _templateDbName = "template-db";

        private static int _awsPort;
        private static int _notificationServicePort;

        public AwsFixture AwsFixture { get; private set; }
        public PostgreSqlFixture PostgreSqlFixture { get; private set; }
        public RabbitMqFixture RabbitMqFixture { get; private set; }
        public SmtpFixture SmtpFixture { get; private set; }

        public NotificationWebAppFactory NotificationWebApp { get; private set; } = null!;

        public WorkerCollectionCluster()
        {
            AwsFixture = new AwsFixture();
            PostgreSqlFixture = new PostgreSqlFixture();
            RabbitMqFixture = new RabbitMqFixture();
            SmtpFixture = new SmtpFixture();
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
                    new Dictionary<string, Type>()
                    {
                        { _templateDbName, typeof(TemplateDbContext) }
                    }),
                RabbitMqFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: _caBundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq"),
                SmtpFixture.InitializeAsync()
            );

            await AwsFixture.AddSecretStringAsync("template/notification-svc/connections", JsonSerializer.Serialize(
                new
                {
                    Connections = new
                    {
                        RabbitMq = RabbitMqFixture.GetConnectionOptions(),
                        TemplateDb = PostgreSqlFixture.GetConnectionOptions(_templateDbName)
                    }
                }));
            await AwsFixture.AddSecretStringAsync("template/cert/ca-bundle", JsonSerializer.Serialize(
                new
                {
                   CABundle = new
                   {
                       Value = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _caBundlePath))
                   }
                }));

            _awsPort = AwsFixture.GetPort();
            _notificationServicePort = KestrelHelper.GetFreePort();

            var smtpConnSettings = SmtpFixture.GetSmtpConnectionSettings();
            NotificationWebApp = new NotificationWebAppFactory(
                new SmtpOptions()
                {
                    Username = "",
                    Password = "",
                    Email = smtpConnSettings.Email,
                    DisplayName = smtpConnSettings.DisplayName,
                    Server = smtpConnSettings.Server,
                    Port = smtpConnSettings.Port,
                    EnableSsl = smtpConnSettings.EnableSsl
                });
            NotificationWebApp.StartServer();
        }

        public async Task DisposeAsync()
        {
            await Task.WhenAll(
                PostgreSqlFixture.DisposeAsync(),
                RabbitMqFixture.DisposeAsync(),
                SmtpFixture.DisposeAsync()
            );
            
            await NotificationWebApp.DisposeAsync();
        }

        public class NotificationWebAppFactory : WebAppFactoryFixture<Program>
        {
            private readonly SmtpOptions _smtpOptions;

            public NotificationWebAppFactory(SmtpOptions smtpOptions) : base(_caBundlePath)
            {
                _smtpOptions = smtpOptions;
            }

            protected override void OverrideConfiguration(IConfigurationBuilder configuration)
            {
                configuration.AddInMemoryCollection([
                    new KeyValuePair<string, string?>("HttpPort",
                        _notificationServicePort.ToString()),

                    new KeyValuePair<string, string?>($"AWS:ServiceURL",
                        $"http://localhost:{_awsPort}"),

                    new KeyValuePair<string, string?>($"{SmtpOptions.Key}:{nameof(SmtpOptions.Username)}",
                        _smtpOptions.Username),
                    new KeyValuePair<string, string?>($"{SmtpOptions.Key}:{nameof(SmtpOptions.Password)}",
                        _smtpOptions.Password),
                    new KeyValuePair<string, string?>($"{SmtpOptions.Key}:{nameof(SmtpOptions.Email)}",
                        _smtpOptions.Email),
                    new KeyValuePair<string, string?>($"{SmtpOptions.Key}:{nameof(SmtpOptions.DisplayName)}",
                        _smtpOptions.DisplayName),
                    new KeyValuePair<string, string?>($"{SmtpOptions.Key}:{nameof(SmtpOptions.Server)}",
                        _smtpOptions.Server),
                    new KeyValuePair<string, string?>($"{SmtpOptions.Key}:{nameof(SmtpOptions.Port)}",
                        _smtpOptions.Port.ToString()),
                    new KeyValuePair<string, string?>($"{SmtpOptions.Key}:{nameof(SmtpOptions.EnableSsl)}",
                        _smtpOptions.EnableSsl.ToString())
                ]);
            }
        }
    }
}
