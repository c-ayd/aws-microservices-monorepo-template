using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.AspNetCore.Helpers.Kestrel.Utilities;
using Shared.PostgreSql.Helpers.Options.Extensions;

namespace Shared.PostgreSql.Helpers.EntityFramework.Extensions
{
    public static partial class EFCoreExtensions
    {
        public static void ConfigureDbContext(this DbContextOptionsBuilder builder,
            PostgreSqlConnectionOptions connectionOptions,
            string? caBundleValue = null)
        {
            var connStringBuilder = new NpgsqlConnectionStringBuilder()
            {
                Host = connectionOptions.Host,
                Port = connectionOptions.Port,
                Database = connectionOptions.Database,
                Username = connectionOptions.Username,
                Password = connectionOptions.Password
            };

            X509Certificate2Collection? caBundle = null;
            if (connectionOptions.Ssl != null)
            {
                connStringBuilder.SslMode = SslMode.Require;
                caBundle = TlsUtility.LoadCertificateChain(caBundleValue!);
            }

            builder.UseNpgsql(connStringBuilder.ConnectionString, npgsqlOptions =>
            {
                if (connectionOptions.Ssl != null)
                {
                    npgsqlOptions.ConfigureDataSource(dataSourceBuilder =>
                    {
                        dataSourceBuilder.UseSslClientAuthenticationOptionsCallback(authOptions =>
                        {
                            authOptions.TargetHost = connectionOptions.Ssl.ServerName;
                            authOptions.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                            {
                                return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle!, sslPolicyErrors);
                            };
                        });
                    });
                }

                if (connectionOptions.RetryMechanism != null)
                {
                    npgsqlOptions.EnableRetryOnFailure(
                        connectionOptions.RetryMechanism.MaxRetry,
                        TimeSpan.FromSeconds(connectionOptions.RetryMechanism.MaxDelayInSeconds),
                        errorCodesToAdd: null);
                }
            });
        }
    }
}
