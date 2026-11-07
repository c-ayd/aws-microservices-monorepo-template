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
        private const int _batchSize = 20;

        private async Task UpdateCertificates(
            BatchGetSecretValueResponse secrets,
            EventJson.CertificatesInfo certificatesInfo,
            DateTimeOffset startDate,
            Dictionary<string, ServiceRotationResult> results,
            ILambdaLogger logger)
        {
            // Deserialize the intermediate CA secret for issuing new certificates
            var intermediateCA = JsonSerializer.Deserialize<IntermediateCA>(secrets.SecretValues
                .Where(s => s.Name == certificatesInfo.IntermediateCASecretId)
                .FirstOrDefault()!
                .SecretString);
            if (intermediateCA == null)
                throw new IntermediateCASecretFormatException();

            var updateTasks = new Dictionary<string, Task>();
            foreach (var service in certificatesInfo.Services)
            {
                // Issue new certificates and create certificates options
                var issuedCertificates = Utility.IssueCertificates(
                    intermediateCA.Certificate,
                    intermediateCA.PrivateKey,
                    intermediateCA.Password,
                    mTls: false,
                    certificatesInfo.Organization,
                    certificatesInfo.Country,
                    service.ServiceName,
                    service.DnsNames,
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
                    SecretId = service.CertificateSecretId,
                    SecretString = JsonSerializer.Serialize(new Dictionary<string, CertificatesOptionsBase>()
                    {
                        { CertificatesOptionsBase.Key, certificatesOptions}
                    })
                }));

                // Wait for the batch to be completed before issuing more certificates
                if (updateTasks.Count >= _batchSize)
                {
                    await UpdateCertificates_WaitForUpdateTasksAsync(updateTasks, results, logger);
                }
            }

            // Wait for the last batch if there is any
            if (updateTasks.Count != 0)
            {
                await UpdateCertificates_WaitForUpdateTasksAsync(updateTasks, results, logger);
            }
        }

        private async Task UpdateCertificates_WaitForUpdateTasksAsync(
            Dictionary<string, Task> updateTasks,
            Dictionary<string, ServiceRotationResult> results,
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
                    if (results.ContainsKey(task.Key))
                    {
                        results[task.Key].Certificates = true;
                    }
                    else
                    {
                        results.Add(task.Key, new ServiceRotationResult()
                        {
                            Certificates = true
                        });
                    }

                    logger.LogInformation($"Certificates ({task.Key}) - The certificates have been updated.");
                }
                else
                {
                    if (results.ContainsKey(task.Key))
                    {
                        results[task.Key].Certificates = false;
                    }
                    else
                    {
                        results.Add(task.Key, new ServiceRotationResult()
                        {
                            Certificates = false
                        });
                    }

                    logger.LogError($"Certificates ({task.Key}) - Something went wrong. Message: {task.Value.Exception?.Message}, Stack Trace: {task.Value.Exception?.StackTrace}");
                }
            }

            updateTasks.Clear();
        }
    }
}
