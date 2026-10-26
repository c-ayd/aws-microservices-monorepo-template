using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Shared.AspNetCore.Helpers.Kestrel.Structures;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;

namespace Shared.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is a web application factory to centralize common functionalities for test cases.
    /// </summary>
    /// <typeparam name="TEntryPoint">Entry point of the web application, usually the main Program.cs</typeparam>
    public abstract class WebAppFactoryFixture<TEntryPoint> : WebApplicationFactory<TEntryPoint>
        where TEntryPoint : class
    {
        private readonly string _defaultPortConfigName;
        private X509Certificate2Collection _caBundle;

        protected List<HttpClient> HttpClients { get; private set; } = new List<HttpClient>();

        public WebAppFactoryFixture(string defaultPortConfigName, string? caBundlePath)
        {
            _defaultPortConfigName = defaultPortConfigName;
            _caBundle = TlsUtility.LoadCertificateChain(
                File.ReadAllText(Path.Combine(AppContext.BaseDirectory, caBundlePath!)));

            // To force WebAppFactory to use Kestrel instead of TestServer, a random port is also
            // opened as plain HTTP. Since there is change that one of the predefined ports might
            // be needed in some test cases, that port is removed from the list.
            UseKestrel(options =>
            {
                options.ListenAnyIP(0);
            });
        }

        protected override IHost CreateHost(IHostBuilder builder)
        {
            builder.ConfigureHostConfiguration(config =>
            {
                OverrideConfiguration(config);
            });

            return base.CreateHost(builder);
        }

        protected virtual void OverrideConfiguration(IConfigurationBuilder configuration)
        {
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
        }

        public int GetPort(string portConfigName)
        {
            var portConfigs = GetService<PortConfigs>();
            return ((IPEndPoint)portConfigs.Ports[portConfigName].Configuration.EndPoint).Port;
        }

        public HttpClient CreateHttpClient(
            string? portConfigName = null,
            Dictionary<string, string>? headers = null,
            Dictionary<string, string>? cookies = null)
        {
            return CreateManagedHttpClient(portConfigName ?? _defaultPortConfigName, headers, cookies);
        }

        protected HttpClient CreateManagedHttpClient(
            string portConfigName,
            Dictionary<string, string>? headers = null,
            Dictionary<string, string>? cookies = null)
        {
            HttpClient client;

            var portConfigs = GetService<PortConfigs>();
            var portConfig = portConfigs.Ports[portConfigName];
            if (portConfig.TlsEnabled)
            {
                client = new HttpClient(new SocketsHttpHandler()
                {
                    SslOptions = new SslClientAuthenticationOptions()
                    {
                        RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                        {
                            return TlsUtility.ValidateServerCertificate(certificate, chain, _caBundle, sslPolicyErrors);
                        }
                    },
                });

                client.BaseAddress = new Uri($"https://localhost:{((IPEndPoint)portConfig.Configuration.EndPoint).Port}");
            }
            else
            {
                client = new HttpClient()
                {
                    BaseAddress = new Uri($"http://localhost:{((IPEndPoint)portConfig.Configuration.EndPoint).Port}")
                };
            }

            if (headers != null)
            {
                foreach (var (key, value) in headers)
                {
                    client.DefaultRequestHeaders.Add(key, value);
                }
            }

            if (cookies != null)
            {
                string cookieString = "";
                foreach (var (key, value) in cookies)
                {
                    cookieString += $"{key}={value}; ";
                }

                client.DefaultRequestHeaders.Add("Cookie", cookieString.Substring(0, cookieString.Length - 2));
            }

            HttpClients.Add(client);
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
