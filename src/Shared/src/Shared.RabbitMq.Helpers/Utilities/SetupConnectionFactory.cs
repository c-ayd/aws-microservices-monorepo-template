using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using RabbitMQ.Client;
using Shared.AspNetCore.Helpers.Tls.Utilities;
using Shared.RabbitMq.Helpers.Options.Extensions;

namespace Shared.RabbitMq.Helpers.Utilities
{
    internal static partial class Utility
    {
        internal static ConnectionFactory SetupConnectionFactory(RabbitMqConnectionOptions connectionOptions)
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

            if (connectionOptions.Ssl != null)
            {
                var caBundle = X509CertificateLoader.LoadCertificateFromFile(connectionOptions.Ssl.CABundlePath);
                connectionFactory.Ssl = new SslOption()
                {
                    Enabled = true,
                    ServerName = connectionOptions.Ssl.ServerName,
                    Version = SslProtocols.Tls12 | SslProtocols.Tls13,
                    AcceptablePolicyErrors = SslPolicyErrors.None,
                    CertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, caBundle);
                    }
                };
            }

            return connectionFactory;
        }
    }
}
