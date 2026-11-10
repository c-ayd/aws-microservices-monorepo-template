using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.SecretsManager.Model;
using Common.Aws.Helpers.Options;
using Lambda.SecretsRotations.Exceptions;
using Lambda.SecretsRotations.Structures;
using Lambda.SecretsRotations.Utilities;

namespace Lambda.SecretsRotations
{
    public partial class Function
    {
        private async Task UpdateCertificatesAsync(
            BatchGetSecretValueResponse secrets,
            EventJson.CertificateInfo certificateInfo,
            List<EventJson.ServiceInfo> services,
            DateTimeOffset startDate,
            Dictionary<string, ServiceRotationResult> serviceResults,
            ILambdaLogger logger)
        {
            // Deserialize the intermediate CA secret for issuing new certificates
            var intermediateCA = JsonSerializer.Deserialize<IntermediateCA>(secrets.SecretValues
                .Where(s => s.Name == certificateInfo.IntermediateCASecretId)
                .Single()
                .SecretString);
            if (intermediateCA == null)
                throw new SecretNotFoundException(certificateInfo.IntermediateCASecretId);

            var updateTasks = new Dictionary<string, Task>();
            foreach (var service in services)
            {
                if (service.Certificate == null)
                    continue;

                // Issue new certificates and create certificates options
                var issuedCertificates = Utility.IssueCertificates(
                    intermediateCA.Certificate,
                    intermediateCA.PrivateKey,
                    intermediateCA.Password,
                    mTls: false,
                    certificateInfo.Organization,
                    certificateInfo.Country,
                    service.ServiceName,
                    service.Certificate.DnsNames,
                    startDate,
                    // In case something goes wrong while rotating the certificates, extra 7 days are given
                    // to the expiration date, so that TLS handshakes between services can continue and
                    // there will be enough time to fix the issue.
                    validDays: 90 + 7);

                var certificatesOptions = new CertificatesOptionsBase()
                {
                    ServerCertificate = issuedCertificates.Server.Certificate,
                    ServerPrivateKey = issuedCertificates.Server.PrivateKey
                };

                // Add task to the list for batch update
                updateTasks.Add(service.ServiceName, AwsSecretsManager.PutSecretValueAsync(new PutSecretValueRequest()
                {
                    SecretId = service.Certificate.CertificateSecretId,
                    SecretString = JsonSerializer.Serialize(new Dictionary<string, CertificatesOptionsBase>()
                    {
                        { CertificatesOptionsBase.Key, certificatesOptions}
                    })
                }));

                // Wait for the batch to be completed before issuing more certificates
                if (updateTasks.Count >= _batchSize)
                {
                    await UpdateCertificates_WaitForUpdateTasksAsync(updateTasks, serviceResults, logger);

                    // Wait 1 second to avoid API throttling
                    await Task.Delay(1000);
                }
            }

            // Wait for the last batch if there is any
            if (updateTasks.Count != 0)
            {
                await UpdateCertificates_WaitForUpdateTasksAsync(updateTasks, serviceResults, logger);
            }
        }

        private async Task UpdateCertificates_WaitForUpdateTasksAsync(
            Dictionary<string, Task> updateTasks,
            Dictionary<string, ServiceRotationResult> serviceResults,
            ILambdaLogger logger)
        {
            try
            {
                await Task.WhenAll(updateTasks.Values);
            }
            catch { }

            foreach (var task in updateTasks)
            {
                if (task.Value.IsCompletedSuccessfully)
                {
                    serviceResults[task.Key].Certificates = true;

                    logger.LogInformation($"Certificates ({task.Key}) - The certificates have been updated.");
                }
                else
                {
                    serviceResults[task.Key].Certificates = false;

                    logger.LogError($"Certificates ({task.Key}) - Something went wrong. Message: {task.Value.Exception?.Message}.");
                }
            }

            updateTasks.Clear();
        }
    }
}
