using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shared.AspNetCore.Helpers.Kestrel;
using Shared.AspNetCore.Helpers.Kestrel.Structures;

namespace Shared.Test.Helpers.Fixtures
{
    public class WebApiFixture : WebAppFactoryFixture<Shared.Test.WebApi.Program>
    {
        public const string HttpsPortConfigName = "HttpsOnly";

        private Action<IConfigurationBuilder>? _addConfiguration;
        private Action<IServiceCollection, IConfiguration>? _configureServices;
        private Action<IApplicationBuilder>? _configureApp;
        private Action<IEndpointRouteBuilder>? _configureEndpoints;

        private int _port;
        private string _serverKeyFilePath;
        private string _serverCertificateFilePath;

        public WebApiFixture(
            int port,
            string caBundlePath,
            string serverKeyFilePath,
            string serverCertificateFilePath)
            : base(caBundlePath)
        {
            _port = port;
            _serverKeyFilePath = Path.Combine(AppContext.BaseDirectory, serverKeyFilePath);
            _serverCertificateFilePath = Path.Combine(AppContext.BaseDirectory, serverCertificateFilePath);
        }

        public async Task InitializeAsync(
            Action<IConfigurationBuilder>? addConfiguration,
            Action<IServiceCollection, IConfiguration>? configureServices,
            Action<IApplicationBuilder>? configureApp,
            Action<IEndpointRouteBuilder>? configureEndpoints)
        {
            _addConfiguration = addConfiguration;
            _configureServices = configureServices;
            _configureApp = configureApp;
            _configureEndpoints = configureEndpoints;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            var portConfigs = new PortConfigs();
            builder.ConfigureKestrel(options =>
            {
                options.ConfigureHttps(
                    HttpsPortConfigName,
                    _serverKeyFilePath,
                    _serverCertificateFilePath,
                    port: _port,
                    HttpProtocols.Http1AndHttp2,
                    portConfigs);
            });

            builder.ConfigureAppConfiguration((context, config) =>
            {
                _addConfiguration?.Invoke(config);
            });

            builder.ConfigureServices((context, services) =>
            {
                services.AddSingleton(portConfigs);
                
                _configureServices?.Invoke(services, context.Configuration);
            });

            builder.Configure(app =>
            {
                app.UseRouting();

                _configureApp?.Invoke(app);
                app.UseEndpoints(endpoints =>
                {
                    _configureEndpoints?.Invoke(endpoints);
                });
            });
        }

        /// <summary>
        /// Is used with Task.WhenAll since ValueTask is not allowed in Task.WhenAll.
        /// </summary>
        public async Task DisposeWebApiAsync()
        {
            await base.DisposeAsync();
        }
    }
}
