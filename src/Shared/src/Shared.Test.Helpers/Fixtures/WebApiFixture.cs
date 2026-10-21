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
        public const string HttpPortConfigName = "Http";
        public const string HttpsPortConfigName = "Https";

        private Action<IConfigurationBuilder>? _overrideConfiguration;
        private Action<IServiceCollection, IConfiguration>? _configureServices;
        private Action<IApplicationBuilder>? _configureApp;
        private Action<IEndpointRouteBuilder>? _configureEndpoints;

        private bool _sslEnabled = false;
        private string? _serverKeyFilePath;
        private string? _serverKeyFileValue;
        private string? _serverCertificateFilePath;
        private string? _serverCertificateFileValue;

        public WebApiFixture(
            bool sslEnabled,
            string? caBundlePath,
            string? serverKeyFilePath,
            string? serverCertificateFilePath)
            : base(caBundlePath)
        {
            _sslEnabled = sslEnabled;
            _serverKeyFilePath = serverKeyFilePath;
            _serverCertificateFilePath = serverCertificateFilePath;
        }

        public async Task InitializeAsync(
            Action<IConfigurationBuilder>? overrideConfiguration,
            Action<IServiceCollection, IConfiguration>? configureServices,
            Action<IApplicationBuilder>? configureApp,
            Action<IEndpointRouteBuilder>? configureEndpoints)
        {
            if (_sslEnabled)
            {
                _serverKeyFileValue = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _serverKeyFilePath!));
                _serverCertificateFileValue = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, _serverCertificateFilePath!));
            }

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
                if (_sslEnabled)
                {
                    options.ConfigureHttps(
                        HttpsPortConfigName,
                        _serverKeyFileValue!,
                        _serverCertificateFileValue!,
                        port: 0,
                        HttpProtocols.Http1AndHttp2,
                        portConfigs);
                }
                else
                {
                    options.ConfigureHttp(
                        HttpPortConfigName,
                        port: 0,
                        HttpProtocols.Http1AndHttp2,
                        portConfigs);
                }
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
