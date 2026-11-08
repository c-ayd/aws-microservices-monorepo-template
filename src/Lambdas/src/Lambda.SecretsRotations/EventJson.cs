namespace Lambda.SecretsRotations
{
    /// <summary>
    /// Holds the information about the 'detail' section of the given input.
    /// </summary>
    public class EventJson
    {
        public required string EcsClusterName { get; set; }
        public required string JwtKeysSecretId { get; set; }
        public required CertificatesInfo Certificates { get; set; }
        public required ConnectionsInfo Connections { get; set; }
        public EnvironmentInfo? Environment { get; set; }

        public class CertificatesInfo
        {
            public required string IntermediateCASecretId { get; set; }
            public required string Organization { get; set; }
            public required string Country { get; set; }
            public required List<ServiceInfo> Services { get; set; }

            public class ServiceInfo
            {
                public required string CertificateSecretId { get; set; }
                public required string ServiceName { get; set; }
                public required List<string> DnsNames { get; set; }
            }
        }

        public class ConnectionsInfo
        {
            public required string CABundleSecretId { get; set; }
            public required string AdminConnectionsSecretId { get; set; }
            public required string OldConnectionCredentialsSecretId { get; set; }
            public required List<ServiceInfo> Services { get; set; }

            public class ServiceInfo
            {
                public required string ConncetionsSecretId { get; set; }
                public required string ServiceName { get; set; }
            }
        }

        public class EnvironmentInfo
        {
            public required bool IsProduction { get; set; }
            public string? ServiceUrl { get; set; }
        }
    }
}
