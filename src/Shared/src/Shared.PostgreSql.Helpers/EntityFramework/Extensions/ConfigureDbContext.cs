using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shared.PostgreSql.Helpers.Options.Extensions;

namespace Shared.PostgreSql.Helpers.EntityFramework.Extensions
{
    public static partial class EFCoreExtensions
    {
        public static void ConfigureDbContext(this DbContextOptionsBuilder builder, PostgreSqlConnectionOptions connectionOptions)
        {
            var connStringBuilder = new NpgsqlConnectionStringBuilder()
            {
                Host = connectionOptions.Host,
                Port = connectionOptions.Port,
                Database = connectionOptions.Database,
                Username = connectionOptions.Username,
                Password = connectionOptions.Password
            };

            if (connectionOptions.Ssl != null)
            {
                connStringBuilder.SslMode = SslMode.VerifyFull;
                connStringBuilder.RootCertificate = connectionOptions.Ssl.CABundlePath;
            }

            builder.UseNpgsql(connStringBuilder.ConnectionString, npgsqlOptions =>
            {
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
