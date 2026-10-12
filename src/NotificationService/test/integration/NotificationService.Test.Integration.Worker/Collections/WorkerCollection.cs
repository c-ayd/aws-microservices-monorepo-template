using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using NotificationService.Worker.DbContexts;
using NotificationService.Worker.Options;
using Shared.AspNetCore.Helpers.Kestrel.Options.Extensions;
using Shared.PostgreSql.Helpers.Options.Extensions;
using Shared.RabbitMq.Helpers.Options.Extensions;
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

        public const string TemplateDbName = "template-db";

        public PostgreSqlFixture PostgreSqlFixture { get; private set; }
        public RabbitMqFixture RabbitMqFixture { get; private set; }
        public SmtpFixture SmtpFixture { get; private set; }

        public NotificationWebAppFactory NotificationWebApp { get; private set; } = null!;

        public WorkerCollectionCluster()
        {
            PostgreSqlFixture = new PostgreSqlFixture();
            RabbitMqFixture = new RabbitMqFixture();
            SmtpFixture = new SmtpFixture();
        }

        public async Task InitializeAsync()
        {
            await Task.WhenAll(
                PostgreSqlFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    certificateKeyFilePath: "Certs/PostgreSql/test-postgresql-server.key",
                    certificateFilePath: "Certs/PostgreSql/test-postgresql-server.crt",
                    new Dictionary<string, Type>()
                    {
                        { TemplateDbName, typeof(TemplateDbContext) }
                    }),
                RabbitMqFixture.InitializeAsync(
                    sslEnabled: true,
                    caBundlePath: CABundlePath,
                    rabbitMqCertificateFolder: "Certs/RabbitMq"),
                SmtpFixture.InitializeAsync()
            );

            var smtpConnSettings = SmtpFixture.GetSmtpConnectionSettings();
            NotificationWebApp = new NotificationWebAppFactory(
                new ConnectionsOptions()
                {
                    RabbitMq = RabbitMqFixture.GetConnectionOptions(),
                    TemplateDb = PostgreSqlFixture.GetConnectionOptions(TemplateDbName)
                },
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
            private readonly ConnectionsOptions _connectionsOptions;
            private readonly SmtpOptions _smtpOptions;

            public NotificationWebAppFactory(
                ConnectionsOptions connectionsOptions,
                SmtpOptions smtpOptions)
                : base(port: 8080, caBundlePath: CABundlePath)
            {
                _connectionsOptions = connectionsOptions;
                _smtpOptions = smtpOptions;
            }

            protected override void ConfigureWebHost(IWebHostBuilder builder)
            {
                builder.Configure(_ => { });

                base.ConfigureWebHost(builder);

                builder.ConfigureAppConfiguration((context, config) =>
                {
                    config.AddInMemoryCollection([
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TemplateDb)}:{nameof(PostgreSqlConnectionOptions.Host)}",
                            _connectionsOptions.TemplateDb.Host),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TemplateDb)}:{nameof(PostgreSqlConnectionOptions.Port)}",
                            _connectionsOptions.TemplateDb.Port.ToString()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TemplateDb)}:{nameof(PostgreSqlConnectionOptions.Database)}",
                            _connectionsOptions.TemplateDb.Database),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TemplateDb)}:{nameof(PostgreSqlConnectionOptions.Username)}",
                            _connectionsOptions.TemplateDb.Username),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TemplateDb)}:{nameof(PostgreSqlConnectionOptions.Password)}",
                            _connectionsOptions.TemplateDb.Password),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TemplateDb)}:{nameof(PostgreSqlConnectionOptions.Ssl)}:{nameof(SslOptions.CABundlePath)}",
                            _connectionsOptions.TemplateDb.Ssl!.CABundlePath),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TemplateDb)}:{nameof(PostgreSqlConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                            _connectionsOptions.TemplateDb.Ssl!.ServerName),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.TemplateDb)}:{nameof(PostgreSqlConnectionOptions.RetryMechanism)}",
                            null),

                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Host)}",
                            _connectionsOptions.RabbitMq.Host),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Port)}",
                            _connectionsOptions.RabbitMq.Port.ToString()),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Username)}",
                            _connectionsOptions.RabbitMq.Username),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Password)}",
                            _connectionsOptions.RabbitMq.Password),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Ssl)}:{nameof(SslOptions.CABundlePath)}",
                            _connectionsOptions.RabbitMq.Ssl!.CABundlePath),
                        new KeyValuePair<string, string?>($"{ConnectionsOptions.Key}:{nameof(ConnectionsOptions.RabbitMq)}:{nameof(RabbitMqConnectionOptions.Ssl)}:{nameof(SslOptions.ServerName)}",
                            _connectionsOptions.RabbitMq.Ssl!.ServerName),

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
                });
            }
        }
    }
}
