using System.Security.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;

namespace Shared.AspNetCore.Helpers.Kestrel
{
    public static partial class KestrelHelper
    {
        public static void ConfigureHttps(this KestrelServerOptions options,
            string serverKeyFilePath,
            string serverCertificateFilePath,
            int port,
            HttpProtocols protocols)
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
            });
        }
    }
}
