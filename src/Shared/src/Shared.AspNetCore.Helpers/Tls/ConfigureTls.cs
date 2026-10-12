using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Shared.AspNetCore.Helpers.Tls.Utilities;

namespace Shared.AspNetCore.Helpers.Tls
{
    public static partial class TlsHelper
    {
        public static void ConfigureTls(this KestrelServerOptions options,
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
