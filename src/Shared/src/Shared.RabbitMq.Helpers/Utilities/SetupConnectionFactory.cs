using System.Net.Security;
using System.Security.Authentication;
using RabbitMQ.Client;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;
using Shared.RabbitMq.Helpers.Options.Extensions;

namespace Shared.RabbitMq.Helpers.Utilities
{
    internal static partial class Utility
    {
        internal static ConnectionFactory SetupConnectionFactory(
            RabbitMqConnectionOptions connectionOptions,
            string? caBundleValue)
        {
            var connectionFactory = new ConnectionFactory()
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
                connectionFactory.Ssl = new SslOption()
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
                    connectionFactory.Ssl.ServerName = connectionOptions.Ssl.ServerName;
                }
            }

            return connectionFactory;
        }
    }
}
