namespace Lambda.SecretsRotations.Structures
{
    /// <summary>
    /// Holds information about a service's certificate and connection rotations if applicable.
    /// </summary>
    public class ServiceRotationResult
    {
        public bool? Certificates { get; set; } = null;
        public bool? Connections { get; set; } = null;

        public bool IsFailed()
        {
            return (Certificates.HasValue && !Certificates.Value) ||
                (Connections.HasValue && !Connections.Value);
        }
    }
}
