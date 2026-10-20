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

        private Action<IConfigurationBuilder>? _overrideConfiguration;
        private Action<IServiceCollection, IConfiguration>? _configureServices;
        private Action<IApplicationBuilder>? _configureApp;
        private Action<IEndpointRouteBuilder>? _configureEndpoints;

        private string _serverKeyFilePath;
        private string _serverCertificateFilePath;

        public WebApiFixture(
            string caBundlePath,
            string serverKeyFilePath,
            string serverCertificateFilePath)
            : base(caBundlePath)
        {
            _serverKeyFilePath = Path.Combine(AppContext.BaseDirectory, serverKeyFilePath);
            _serverCertificateFilePath = Path.Combine(AppContext.BaseDirectory, serverCertificateFilePath);
        }

        public async Task InitializeAsync(
            Action<IConfigurationBuilder>? overrideConfiguration,
            Action<IServiceCollection, IConfiguration>? configureServices,
            Action<IApplicationBuilder>? configureApp,
            Action<IEndpointRouteBuilder>? configureEndpoints)
        {
            _overrideConfiguration = overrideConfiguration;
            _configureServices = configureServices;
            _configureApp = configureApp;
            _configureEndpoints = configureEndpoints;
        }

        protected override void OverrideConfiguration(IConfigurationBuilder configuration)
        {
            _overrideConfiguration?.Invoke(configuration);
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
                    port: 0,
                    HttpProtocols.Http1AndHttp2,
                    portConfigs);
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
