namespace Lambda.SecretsRotations
{
    /// <summary>
    /// Holds the information about the 'detail' section of the given input.
    /// </summary>
    public class EventJson
    {
        public required string EcsClusterName { get; set; }
        public required string JwtKeysSecretId { get; set; }
        public required CertificateInfo Certificate { get; set; }
        public required ConnectionInfo Connection { get; set; }
        public required List<ServiceInfo> Services { get; set; }
        public EnvironmentInfo? Environment { get; set; }

        public class CertificateInfo
        {
            public required string IntermediateCASecretId { get; set; }
            public required string CABundleSecretId { get; set; }
            public required string Organization { get; set; }
            public required string Country { get; set; }
        }

        public class ConnectionInfo
        {
            public required string AdminConnectionsSecretId { get; set; }
            public required string OldUsersSecretId { get; set; }
        }

        public class ServiceInfo
        {
            public required string ServiceName { get; set; }
            public CertificateUpdateInfo? Certificate { get; set; }
            public ConnectionUpdateInfo? Connection { get; set; }

            public class CertificateUpdateInfo
            {
                public required string CertificateSecretId { get; set; }
                public required List<string> DnsNames { get; set; }
            }

            public class ConnectionUpdateInfo
            {
                public required string ConncetionsSecretId { get; set; }
            }
        }

        public class EnvironmentInfo
        {
            public required bool IsProduction { get; set; }
            public string? ServiceUrl { get; set; }
        }
    }
}
