using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Amazon.Lambda.Core;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.PostgreSql.Helpers.Options.Extensions;
using Lambda.DeleteOldConnections.Structures;
using Npgsql;
using Shared.Lambda.Structures;

namespace Lambda.DeleteOldConnections
{
    public partial class Function
    {
        private async Task DeletePostgreSqlCredentialsAsync(
            PostgreSqlConnectionOptions connectionOptions,
            X509Certificate2Collection caBundle,
            List<OldCredentials.PostgreSqlCredentials> credentialsToDelete,
            DeletionResults deletionResults,
            ILambdaLogger logger)
        {
            var connStringBuilder = new NpgsqlConnectionStringBuilder()
            {
                Host = connectionOptions.Host,
                Port = connectionOptions.Port,
                Database = connectionOptions.Database,
                Username = connectionOptions.Username,
                Password = connectionOptions.Password,
                SslMode = SslMode.Require
            };

            var builder = new NpgsqlDataSourceBuilder(connStringBuilder.ConnectionString);
            builder.UseSslClientAuthenticationOptionsCallback(options =>
            {
                options.EnabledSslProtocols = SslProtocols.Tls12 | SslProtocols.Tls13;
                options.TargetHost = connectionOptions.Ssl.ServerName;
                options.RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                {
                    return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                };
            });

            await using var dataSource = builder.Build();
            await using var connection = await dataSource.OpenConnectionAsync();

            foreach (var credentials in credentialsToDelete)
            {
                var id = $"{credentials.Database}/{credentials.Username}";
                
                await using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Stop new logins
                    await using (var command = new NpgsqlCommand(
                        $"ALTER ROLE \"{credentials.Username}\" WITH NOLOGIN;",
                        connection,
                        transaction))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    // Close all sessions
                    await using (var command = new NpgsqlCommand(
                        $@"SELECT pg_terminate_backend(pid)
                        FROM pg_stat_activity
                        WHERE usename = '{credentials.Username}'
                            AND datname = '{credentials.Database}'
                            AND pid <> pg_backend_pid();",
                        connection,
                        transaction))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    // Delete role
                    await using (var command = new NpgsqlCommand(
                        $"DROP ROLE \"{credentials.Username}\";",
                        connection,
                        transaction))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    deletionResults.DeletedPostgreSqlCredentials.Add(id);

                    logger.LogInformation($"PostgreSQL ({id}) - The credentials have been deleted.");
                }
                catch (Exception exception)
                {
                    await transaction.RollbackAsync();

                    deletionResults.FailedPostgreSqlCredentials.Add(id);

                    logger.LogError(exception, $"PostgreSQL ({id}) - Something went wrong. Message: {exception.Message}.");
                }
            }
        }
    }
}
