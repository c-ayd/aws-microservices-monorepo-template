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
        private async Task DeleteRabbitMqCredentialsAsync(
            RabbitMqConnectionOptions connectionOptions,
            X509Certificate2Collection caBundle,
            EventJson.EnvironmentInfo? environmentInfo,
            List<string> credentialsToDelete,
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

            foreach (var credentials in credentialsToDelete)
            {
                // The deletion of credentials in RabbitMQ closes the open sessions automatically.
                var deleteResponse = await httpClient.DeleteAsync($"/api/users/{credentials}");

                if (deleteResponse.IsSuccessStatusCode)
                {
                    deletionResults.DeletedRabbitMqCredentials.Add(credentials);

                    logger.LogInformation($"RabbitMQ ({credentials}) - The credentials have been deleted.");
                }
                else
                {
                    deletionResults.FailedRabbitMqCredentials.Add(credentials);

                    logger.LogError($"RabbitMQ ({credentials}) - Something went wrong. Status Code: {deleteResponse.StatusCode}.");
                }
            }
        }
    }
}
