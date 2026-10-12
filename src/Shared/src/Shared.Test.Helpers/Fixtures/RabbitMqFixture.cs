using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using DotNet.Testcontainers.Builders;
using RabbitMQ.Client;
using Shared.AspNetCore.Helpers.Tls.Options.Extensions;
using Shared.AspNetCore.Helpers.Tls.Utilities;
using Shared.RabbitMq.Helpers.Options.Extensions;
using Testcontainers.RabbitMq;

namespace Shared.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is a RabbitMQ handler for test cases requiring RabbitMQ.
    /// </summary>
    public class RabbitMqFixture
    {
        private RabbitMqContainer _container = null!;

        private ConnectionFactory _connectionFactory { get; set; } = null!;
        public IConnection Connection { get; private set; } = null!;
        public IChannel Channel { get; private set; } = null!;

        private bool _sslEnabled = false;
        private string? _caBundlePath = null;

        public async Task InitializeAsync(
            bool sslEnabled,
            string? caBundlePath = null,
            string? rabbitMqCertificateFolder = null)
        {
            var builder = new RabbitMqBuilder("rabbitmq:4.3.4-management")
                .WithPortBinding(15672, true)
                .WithEnvironment("RABBITMQ_DEFAULT_USER", "guest")
                .WithEnvironment("RABBITMQ_DEFAULT_PASS", "guest")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(".*Server startup complete.*"));

            if (sslEnabled)
            {
                _sslEnabled = true;
                _caBundlePath = Path.Combine(AppContext.BaseDirectory, caBundlePath!);
                _container = builder
                    .WithPortBinding(5671, true)
                    .WithResourceMapping(
                        Path.Combine(AppContext.BaseDirectory, rabbitMqCertificateFolder!),
                        "/etc/rabbitmq/")
                    .Build();
            }
            else
            {
                _sslEnabled = false;
                _caBundlePath = null;
                _container = builder
                    .WithPortBinding(5672, true)
                    .Build();
            }

            await _container.StartAsync();

            var connectionOptions = GetConnectionOptions();
            _connectionFactory = new ConnectionFactory()
            {
                UserName = connectionOptions.Username,
                Password = connectionOptions.Password,
                HostName = connectionOptions.Host,
                Port = connectionOptions.Port
            };

            if (_sslEnabled)
            {
                var caBundle = X509CertificateLoader.LoadCertificateFromFile(_caBundlePath!);

                _connectionFactory.Ssl = new SslOption()
                {
                    Enabled = true,
                    ServerName = connectionOptions.Ssl!.ServerName!,
                    Version = SslProtocols.Tls12 | SslProtocols.Tls13,
                    AcceptablePolicyErrors = SslPolicyErrors.None,
                    CertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, caBundle!);
                    }
                };
            }

            Connection = await _connectionFactory.CreateConnectionAsync();
            Channel = await Connection.CreateChannelAsync(new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true));
        }

        public RabbitMqConnectionOptions GetConnectionOptions()
        {
            return new RabbitMqConnectionOptions()
            {
                Host = _container.Hostname,
                Port = _container.GetMappedPublicPort(_sslEnabled ? 5671 : 5672),
                Username = "guest",
                Password = "guest",
                Ssl = !_sslEnabled ? null : new SslOptions()
                {
                    CABundlePath = _caBundlePath!,
                    ServerName = "localhost"
                }
            };
        }

        public async Task PublishMessageAsync(string exchangeName, string routingKey, BasicProperties properties, byte[] body)
        {
            await Channel.BasicPublishAsync(exchangeName, routingKey, mandatory: true, properties, body);
        }

        public async Task<BasicGetResult?> GetNextMessageAsync(string queueName, bool autoAck = true)
        {
            return await Channel.BasicGetAsync(queueName, autoAck);
        }

        public async Task<uint> GetMessageCountAsync(string queueName)
        {
            var queueInfo = await Channel.QueueDeclarePassiveAsync(queueName);
            return queueInfo.MessageCount;
        }

        public async Task ClearMessagesAsync(string queueName)
        {
            await Channel.QueuePurgeAsync(queueName);
        }

        public async Task DisposeAsync()
        {
            if (Channel.IsOpen)
            {
                await Channel.CloseAsync();
            }
            if (Connection.IsOpen)
            {
                await Connection.CloseAsync();
            }
            await _container.StopAsync();

            await Channel.DisposeAsync();
            await Connection.DisposeAsync();
            await _container.DisposeAsync();
        }
    }
}
