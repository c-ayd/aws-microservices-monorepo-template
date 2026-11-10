using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Amazon.Lambda.Core;
using Common.AspNetCore.Helpers.Kestrel.Utilities;
using Common.RabbitMq.Helpers.Options.Extensions;
using Lambda.DeleteOldConnections.Structures;
using Microsoft.Net.Http.Headers;

namespace Lambda.DeleteOldConnections
{
    public partial class Function
    {
        private async Task DeleteRabbitMqUsersAsync(
            RabbitMqConnectionOptions connectionOptions,
            X509Certificate2Collection caBundle,
            EventJson.EnvironmentInfo? environmentInfo,
            List<string> users,
            DeletionResults deletionResults,
            ILambdaLogger logger)
        {
            using var httpClient = new HttpClient(new SocketsHttpHandler()
            {
                SslOptions = new SslClientAuthenticationOptions()
                {
                    RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) =>
                    {
                        if ((environmentInfo != null && !environmentInfo.IsProduction) &&
                            ((sslPolicyErrors & SslPolicyErrors.RemoteCertificateNameMismatch) != 0))
                        {
                            sslPolicyErrors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
                        }

                        return TlsUtility.ValidateServerCertificate(certificate, chain, caBundle, sslPolicyErrors);
                    }
                },
            });
            httpClient.BaseAddress = new Uri($"https://{connectionOptions.Host}:{connectionOptions.Port}");
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization, "Basic " +
                Convert.ToBase64String(Encoding.UTF8.GetBytes($"{connectionOptions.Username}:{connectionOptions.Password}")));

            foreach (var user in users)
            {
                // The deletion of a user in RabbitMQ closes the open sessions automatically.
                var deleteResponse = await httpClient.DeleteAsync($"/api/users/{user}");

                if (deleteResponse.IsSuccessStatusCode)
                {
                    deletionResults.DeletedRabbitMqUsers.Add(user);

                    logger.LogInformation($"RabbitMQ ({user}) - The user has been deleted.");
                }
                else
                {
                    deletionResults.FailedRabbitMqUsers.Add(user);

                    logger.LogError($"RabbitMQ ({user}) - Something went wrong. Status Code: {deleteResponse.StatusCode}.");
                }
            }
        }
    }
}
