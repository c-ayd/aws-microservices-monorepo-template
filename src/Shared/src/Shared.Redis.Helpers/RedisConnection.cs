using System.Net.Security;
using System.Security.Authentication;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;
using Shared.Redis.Helpers.Options.Extensions;
using StackExchange.Redis;

namespace Shared.Redis.Helpers
{
    /// <summary>
    /// Handles Redis connection.
    /// </summary>
    public abstract class RedisConnection
    {
        private readonly ConfigurationOptions _configOptions;
        internal ConnectionMultiplexer? Connection { get; private set; }
        private SemaphoreSlim _connectionSemaphore = new SemaphoreSlim(1, 1);

        public RedisConnection(
            RedisConnectionOptions connectionOptions,
            string? caBundleValue = null)
        {
            _configOptions = new ConfigurationOptions()
            {
                EndPoints = { $"{connectionOptions.Host}:{connectionOptions.Port}" },
                Password = connectionOptions.Password,
                AbortOnConnectFail = connectionOptions.AbortOnConnectionFail
            };

            if (connectionOptions.Ssl.Enabled)
            {
                var caBundle = TlsUtility.LoadCertificateChain(caBundleValue!);
                
                _configOptions.Ssl = true;
                _configOptions.SslClientAuthenticationOptions = _ => new SslClientAuthenticationOptions()
                {
                    ClientCertificates = null,
                    EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13,
                    TargetHost = connectionOptions.Ssl.ServerName,
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                    }
                };
            }
        }

        internal async Task ConnectAsync(CancellationToken cancellationToken)
        {
            // This method should be called only once. This semaphore is used as a safeguard.
            await _connectionSemaphore.WaitAsync(cancellationToken);

            if (Connection != null)
            {
                _connectionSemaphore.Release();
                return;
            }

            // Since the connection to Redis would be required for services to function, the code below
            // is not wrapped with a catch block and should throw an exception if something goes wrong.
            try
            {
                Connection = await ConnectionMultiplexer.ConnectAsync(_configOptions);
            }
            finally
            {
                _connectionSemaphore.Release();
            }
        }

        /// <summary>
        /// Gets a database with a given index in the Redis instance.
        /// </summary>
        /// <param name="db">Index of the database. -1 for the default database</param>
        /// <returns>Returns the database with the given index in the Redis instance.</returns>
        protected IDatabase GetDatabase(int db = -1)
        {
            return Connection!.GetDatabase(db);
        }
    }
}
