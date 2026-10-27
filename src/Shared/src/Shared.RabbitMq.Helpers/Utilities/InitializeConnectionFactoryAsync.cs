using System.Net.Security;
using System.Security.Authentication;
using RabbitMQ.Client;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;
using Shared.RabbitMq.Helpers.Options.Extensions;

namespace Shared.RabbitMq.Helpers.Utilities
{
    public static partial class Utility
    {
        private static ConnectionFactory? _connectionFactory = null;
        private static SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Sets the properties of a given <see cref="RabbitMQ.Client.ConnectionFactory"/> instance
        /// and sets the TLS for HTTPS. It does not set up automatic recovery.
        /// </summary>
        /// <param name="connectionOptions">Connection options for RabbitMQ</param>
        /// <param name="caBundleValue">Value of the CA Bundle for TLS</param>
        /// <returns>Returns the created connection factory.</returns>
        public static async Task<ConnectionFactory> InitializeConnectionFactoryAsync(
            RabbitMqConnectionOptions connectionOptions,
            string? caBundleValue,
            CancellationToken cancellationToken)
        {
            await _connectionSemaphore.WaitAsync(cancellationToken);

            if (_connectionFactory != null)
            {
                _connectionSemaphore.Release();
                return _connectionFactory;
            }

            try
            {
                _connectionFactory = new ConnectionFactory()
                {
                    HostName = connectionOptions.Host,
                    Port = connectionOptions.Port,
                    UserName = connectionOptions.Username,
                    Password = connectionOptions.Password,
                    AutomaticRecoveryEnabled = false,
                    TopologyRecoveryEnabled = false
                };

                if (connectionOptions.Ssl.Enabled)
                {
                    var caBundle = TlsUtility.LoadCertificateChain(caBundleValue!);
                    _connectionFactory.Ssl = new SslOption()
                    {
                        Enabled = true,
                        Version = SslProtocols.Tls12 | SslProtocols.Tls13,
                        AcceptablePolicyErrors = SslPolicyErrors.None,
                        CertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                        {
                            return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                        }
                    };

                    if (connectionOptions.Ssl.ServerName != null)
                    {
                        _connectionFactory.Ssl.ServerName = connectionOptions.Ssl.ServerName;
                    }
                }
            }
            finally
            {
                _connectionSemaphore.Release();
            }

            return _connectionFactory;
        }
    }
}
