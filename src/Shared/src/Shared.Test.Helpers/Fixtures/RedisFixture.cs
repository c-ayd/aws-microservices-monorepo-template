using System.Net.Security;
using System.Security.Authentication;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Shared.AspNetCore.Helpers.Kestrel.Options.Extensions;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;
using Shared.Redis.Helpers.Options.Extensions;
using Shared.Test.Helpers.Fixtures.SeedWork;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Shared.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is a Redis handler for test cases requiring Redis.
    /// </summary>
    public class RedisFixture : ContainerFixtureBase
    {
        protected override IContainer Container => _container;
        private RedisContainer _container = null!;
        private ConnectionMultiplexer _connection = null!;

        private bool _sslEnabled = false;
        private string? _caBundleValue = null;

        public async Task InitializeAsync(
            bool sslEnabled,
            string? caBundlePath = null,
            string? redisCertificateFolder = null)
        {
            if (IsInitialized)
                return;
            
            var builder = new RedisBuilder("redis:8.10")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"));

            if (sslEnabled)
            {
                _sslEnabled = true;
                _caBundleValue = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, caBundlePath!));
                _container = builder
                    .WithPortBinding(6380, true)
                    .WithResourceMapping(
                        Path.Combine(AppContext.BaseDirectory, redisCertificateFolder!),
                        "/etc/redis/")
                    .WithCommand(
                        "redis-server",
                        "--tls-port", "6380",
                        "--port", "0",
                        "--requirepass", "redis-password",
                        "--tls-key-file", "/etc/redis/test-redis-server.key",
                        "--tls-cert-file", "/etc/redis/test-redis-server.crt",
                        "--tls-auth-clients", "no")
                    .Build();
            }
            else
            {
                _sslEnabled = false;
                _caBundleValue = null;
                _container = builder
                    .WithPortBinding(6379, true)
                    .WithCommand(
                        "redis-server",
                        "--port", "6379",
                        "--requirepass", "redis-password")
                    .Build();
            }

            await _container.StartAsync();

            var redisConnSettings = GetConnectionOptions();
            var configOptions = new ConfigurationOptions()
            {
                EndPoints = { $"{redisConnSettings.Host}:{redisConnSettings.Port}" },
                Password = redisConnSettings.Password,
                AbortOnConnectFail = redisConnSettings.AbortOnConnectionFail
            };

            if (sslEnabled)
            {
                var caBundle = TlsUtility.LoadCertificateChain(_caBundleValue!);

                configOptions.Ssl = true;
                configOptions.SslClientAuthenticationOptions = _ => new SslClientAuthenticationOptions()
                {
                    ClientCertificates = null,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    TargetHost = redisConnSettings.Ssl!.ServerName,
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                    }
                };
            }

            _connection = await ConnectionMultiplexer.ConnectAsync(configOptions);

            IsInitialized = true;
        }

        public RedisConnectionOptions GetConnectionOptions()
        {
            return new RedisConnectionOptions()
            {
                Host = _container.Hostname,
                Port = _container.GetMappedPublicPort(_sslEnabled ? 6380 : 6379),
                Password = "redis-password",
                AbortOnConnectionFail = true,  // For tests, it throws an exception immediately when the connection is initially not established
                Ssl = new SslOptions()
                {
                    Enabled = _sslEnabled,
                    ServerName = "localhost"
                }
            };
        }

        public IDatabase GetDatabase(int db = -1)
        {
            return _connection.GetDatabase(db);
        }
        
        public async Task DisposeAsync()
        {
            await _connection.CloseAsync();
            await _container.StopAsync();

            await _connection.DisposeAsync();
            await _container.DisposeAsync();
        }
    }
}
