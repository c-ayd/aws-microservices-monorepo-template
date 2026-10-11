using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.AspNetCore.Helpers.Tls.Utilities;
using Shared.Constants;
using Shared.Http.Authentication.Constants;

namespace Shared.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is a web application factory to centralize common functionalities for test cases.
    /// </summary>
    /// <typeparam name="TEntryPoint">Entry point of the web application, usually the main Program.cs</typeparam>
    public class WebAppFactoryFixture<TEntryPoint> : WebApplicationFactory<TEntryPoint>
        where TEntryPoint : class
    {
        private static List<int> _availablePorts = Enumerable.Range(8000, 8080).ToList();

        private readonly int _port = 8111;
        private X509Certificate2 _caBundle;

        protected List<HttpClient> HttpClients { get; private set; } = new List<HttpClient>();

        public WebAppFactoryFixture(int port, string caBundlePath)
        {
            _port = port;
            _caBundle = X509CertificateLoader.LoadCertificateFromFile(Path.Combine(AppContext.BaseDirectory, caBundlePath));

            // To force WebAppFactory to use Kestrel instead of TestServer, a random port is also
            // opened as plain HTTP. Since there is change that one of the predefined ports might
            // be needed in some test cases, that port is removed from the list.
            _availablePorts.Remove(_port);

            var randomPort = _availablePorts[Random.Shared.Next(_availablePorts.Count)];
            _availablePorts.Remove(randomPort);
            
            UseKestrel(options =>
            {
                options.ListenAnyIP(randomPort);
            });
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }

        public HttpClient CreateHttpsClient()
        {
            var client = new HttpClient(new SocketsHttpHandler()
            {
                SslOptions = new SslClientAuthenticationOptions()
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, _caBundle);
                    }
                }
            });

            client.BaseAddress = new Uri($"https://localhost:{_port}");

            HttpClients.Add(client);

            return client;
        }

        public HttpClient CreateHttpsClientWithCredentials(
            string accountId,
            string preferredLanguage = SupportedLanguages.DefaultLanguage,
            Dictionary<string, string>? cookieValues = null)
        {
            var client = CreateHttpsClient();
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.Id.HeaderKey, accountId);
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.PreferredLanguage.HeaderKey, preferredLanguage);
            client.DefaultRequestHeaders.Add(ApiGatewayAuthKeys.Claims.IssuedAt.HeaderKey, DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

            if (cookieValues != null)
            {
                string cookieString = "";
                foreach (var (key, value) in cookieValues)
                {
                    cookieString += $"{key}={value}; ";
                }

                client.DefaultRequestHeaders.Add("Cookie", cookieString.Substring(0, cookieString.Length - 2));
            }

            return client;
        }

        public TService GetService<TService>()
            where TService : notnull
        {
            return Services.GetRequiredService<TService>();
        }

        public AsyncServiceScope GetScopedService<TService>(out TService service)
            where TService : notnull
        {
            var scope = Services.CreateAsyncScope();
            service = scope.ServiceProvider.GetRequiredService<TService>();

            return scope;
        }

        public TBackgroundService GetBackgroundService<TBackgroundService>()
        {
            return Services.GetServices<IHostedService>()
                .OfType<TBackgroundService>()
                .Single();
        }

        public TOptions GetOptions<TOptions>()
            where TOptions: class
        {
            return Services.GetRequiredService<IOptions<TOptions>>().Value;
        }

        public override async ValueTask DisposeAsync()
        {
            // The disposal works on local developments, however, it randomly fails on GitHub in GitHub Actions.
            // Since this method is called once all tests have run, the current solution is to catch the exception
            // and do nothing. The finished job in GitHub Action will clean the resources if there is any.
            try
            {
                foreach (var client in HttpClients)
                {
                    client.Dispose();
                }

                await base.DisposeAsync();
            }
            catch { }
        }
    }
}
