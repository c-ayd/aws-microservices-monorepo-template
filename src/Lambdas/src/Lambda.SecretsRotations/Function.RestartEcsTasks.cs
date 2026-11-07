using Amazon.Lambda.Core;
using Lambda.SecretsRotations.Structures;

namespace Lambda.SecretsRotations
{
    public partial class Function
    {
        private async Task RestartEcsTasks(
            string ecsClusterName,
            Dictionary<string, ServiceRotationResult> results,
            ILambdaLogger logger)
        {
            // Restart services gradually
            foreach (var (serviceName, rotationResult) in results)
            {
                if (rotationResult.IsFailed())
                {
                    logger.LogWarning($"ECS - {serviceName} is being skipped since there are some errors.");
                    continue;
                }

                logger.LogInformation($"ECS - Restarting {serviceName}...");

                await _awsEcs.UpdateServiceAsync(new Amazon.ECS.Model.UpdateServiceRequest()
                {
                    Cluster = ecsClusterName,
                    Service = serviceName,
                    ForceNewDeployment = true
                });
            }
        }
    }
}
