using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using DotNet.Testcontainers.Builders;
using Shared.AspNetCore.Helpers.Tls.Utilities;
using Shared.Redis.Options.Extensions;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Shared.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is a Redis handler for test cases requiring Redis.
    /// </summary>
    public class RedisFixture
    {
        private RedisContainer _container = null!;
        private ConnectionMultiplexer _connection = null!;

        private bool _sslEnabled = false;
        private string? _caBundlePath = null;

        public async Task InitializeAsync(
            bool sslEnabled,
            string? caBundlePath = null,
            string? redisCertificateFolder = null)
        {
            var builder = new RedisBuilder("redis:8.10")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Ready to accept connections"));

            if (sslEnabled)
            {
                _sslEnabled = true;
                _caBundlePath = Path.Combine(AppContext.BaseDirectory, caBundlePath!);
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
                _caBundlePath = null;
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
                var caBundle = X509CertificateLoader.LoadCertificateFromFile(_caBundlePath!);

                configOptions.Ssl = true;
                configOptions.SslClientAuthenticationOptions = _ => new SslClientAuthenticationOptions()
                {
                    ClientCertificates = null,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    TargetHost = redisConnSettings.Host,
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, caBundle);
                    }
                };
            }

            _connection = await ConnectionMultiplexer.ConnectAsync(configOptions);
        }

        public RedisConnectionOptions GetConnectionOptions()
        {
            return new RedisConnectionOptions()
            {
                Host = _container.Hostname,
                Port = _container.GetMappedPublicPort(_sslEnabled ? 6380 : 6379),
                Password = "redis-password",
                AbortOnConnectionFail = false,  // For tests, it throws an exception immediately when the connection is initially not established
                Ssl = !_sslEnabled ? null : new RedisConnectionOptions.SslOptions()
                {
                    CABundlePath = _caBundlePath!
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
