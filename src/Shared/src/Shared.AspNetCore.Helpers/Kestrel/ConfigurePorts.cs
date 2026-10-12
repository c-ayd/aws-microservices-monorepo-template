using System.Security.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Shared.AspNetCore.Helpers.Kestrel.Structures;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;

namespace Shared.AspNetCore.Helpers.Kestrel
{
    public static partial class KestrelHelper
    {
        public static void ConfigureHttp(this KestrelServerOptions options,
            string name,
            int port,
            PortConfigs portConfigs)
        {
            options.ListenAnyIP(port, config =>
            {
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

        public static void ConfigureHttps(this KestrelServerOptions options,
            string name,
            string serverKeyFilePath,
            string serverCertificateFilePath,
            int port,
            HttpProtocols protocols,
            PortConfigs portConfigs)
        {
            var serverCertificate = TlsUtility.LoadCertificateFromFile(serverKeyFilePath, serverCertificateFilePath);
            var serverCertificateChain = TlsUtility.LoadCertificateChainFromFile(serverCertificateFilePath, skipFirstInChain: true);

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
