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
        private async Task DeletePostgreSqlUsersAsync(
            PostgreSqlConnectionOptions connectionOptions,
            X509Certificate2Collection caBundle,
            List<OldConnections.PostgreSqlUser> users,
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

            foreach (var user in users)
            {
                var id = $"{user.Database}/{user.Username}";
                
                await using var transaction = await connection.BeginTransactionAsync();

                try
                {
                    // Stop new logins
                    await using (var command = new NpgsqlCommand(
                        $"ALTER ROLE \"{user.Username}\" WITH NOLOGIN;",
                        connection,
                        transaction))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    // Close all sessions
                    await using (var command = new NpgsqlCommand(
                        $@"SELECT pg_terminate_backend(pid)
                        FROM pg_stat_activity
                        WHERE usename = '{user.Username}'
                            AND datname = '{user.Database}'
                            AND pid <> pg_backend_pid();",
                        connection,
                        transaction))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    // Delete user
                    await using (var command = new NpgsqlCommand(
                        $"DROP ROLE \"{user.Username}\";",
                        connection,
                        transaction))
                    {
                        await command.ExecuteNonQueryAsync();
                    }

                    await transaction.CommitAsync();

                    deletionResults.DeletedPostgreSqlUsers.Add(id);

                    logger.LogInformation($"PostgreSQL ({id}) - The user has been deleted.");
                }
                catch (Exception exception)
                {
                    await transaction.RollbackAsync();

                    deletionResults.FailedPostgreSqlUsers.Add(id);

                    logger.LogError(exception, $"PostgreSQL ({id}) - Something went wrong. Message: {exception.Message}.");
                }
            }
        }
    }
}
