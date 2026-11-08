using System.Net.Security;
using System.Security.Authentication;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.Redis.Helpers.Options.Extensions;
using Common.Test.Helpers.Fixtures.SeedWork;
using StackExchange.Redis;
using Testcontainers.Redis;

namespace Common.Test.Helpers.Fixtures
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
                        "--user", "default", "on", ">123456", "~*", "+@all",
                        "--user", "redis-user", "on", ">redis-password", "~*", "+@all", "-@dangerous",
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
                        "--user", "default", "on", ">123456", "~*", "+@all",
                        "--user", "redis-user", "on", ">redis-password", "~*", "+@all", "-@dangerous")
                    .Build();
            }

            await _container.StartAsync();

            var redisConnSettings = GetConnectionOptions();
            var configOptions = new ConfigurationOptions()
            {
                EndPoints = { $"{redisConnSettings.Host}:{redisConnSettings.Port}" },
                User = "default",
                Password = "123456",
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
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0)
                        {
                            sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
                        }
                        
                        return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
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
                Username = "redis-user",
                Password = "redis-password",
                AbortOnConnectionFail = true, // For tests, it throws an exception immediately when the connection is initially not established
                SslEnabled = _sslEnabled,
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
