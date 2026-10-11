using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.AspNetCore.Server.Kestrel.Https;

namespace Shared.AspNetCore.Helpers.Tls
{
    public static partial class TlsHelper
    {
        public static void ConfigureTls(this KestrelServerOptions options,
            X509Certificate2 serverCertificate,
            int port,
            HttpProtocols protocols)
        {
            options.ListenAnyIP(port, config =>
            {
                config.Protocols = protocols;
                config.UseHttps(serverCertificate, httpsOptions =>
                {
                    httpsOptions.SslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                    httpsOptions.ClientCertificateMode = ClientCertificateMode.NoCertificate;
                });
            });
        }
    }
}
