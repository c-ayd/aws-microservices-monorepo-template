using System.Security.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Common.AspNetCore.Helpers.Kestrel.Structures;
using Common.AspNetCore.Helpers.Kestrel.Utilities;

namespace Common.AspNetCore.Helpers.Kestrel
{
    public static partial class KestrelHelper
    {
        /// <summary>
        /// Configures Kestrel for an HTTP port.
        /// </summary>
        /// <param name="name">Unique name of the port</param>
        /// <param name="port">Desired port</param>
        /// <param name="protocols">Allowed protocols for the port</param>
        /// <param name="portConfigs">Variable that is used to save the configuration of the opened port</param>
        public static void ConfigureHttp(this KestrelServerOptions options,
            string name,
            int port,
            HttpProtocols protocols,
            PortConfigs portConfigs)
        {
            options.ListenAnyIP(port, config =>
            {
                config.Protocols = protocols;

                // Since ports conflict with each other in tests when Kestrel is used,
                // the port is given as 0 during tets and is resolved during runtime.
                // The config is also saved to the portConfigs variable to find and
                // use the port for HttpClients.
                portConfigs.ports.Add(name, new PortConfigs.PortConfig()
                {
                    TlsEnabled = false,
                    Configuration = config
                });
            });
        }

        /// <summary>
        /// Configures Kestrel for an HTTPS port.
        /// </summary>
        /// <param name="name">Unique name of the port</param>
        /// <param name="serverCertificateValue">Value of the certificate of the application</param>
        /// <param name="serverPrivateKeyValue">Value of the private key for the server certificate</param>
        /// <param name="port">Desired port</param>
        /// <param name="protocols">Allowed protocols for the port</param>
        /// <param name="portConfigs">Variable that is used to save the configuration of the opened port</param>
        public static void ConfigureHttps(this KestrelServerOptions options,
            string name,
            string serverCertificateValue,
            string serverPrivateKeyValue,
            int port,
            HttpProtocols protocols,
            PortConfigs portConfigs)
        {
            var serverCertificate = TlsUtility.LoadCertificate(serverCertificateValue, serverPrivateKeyValue);
            var serverCertificateChain = TlsUtility.LoadCertificateChain(serverCertificateValue, skipFirstInChain: true);

            options.ListenAnyIP(port, config =>
            {
                config.Protocols = protocols;
                config.UseHttps(serverCertificate, httpsOptions =>
                {
                    httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                    httpsOptions.ServerCertificateChain = serverCertificateChain;
                    httpsOptions.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                });

                // Since ports conflict with each other in tests when Kestrel is used,
                // the port is given as 0 during tets and is resolved during runtime.
                // The config is also saved to the portConfigs variable to find and
                // use the port for HttpClients.
                portConfigs.ports.Add(name, new PortConfigs.PortConfig()
                {
                    TlsEnabled = true,
                    Configuration = config
                });
            });
        }
    }
}
