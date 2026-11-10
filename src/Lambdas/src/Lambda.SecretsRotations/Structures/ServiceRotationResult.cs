namespace Lambda.SecretsRotations.Structures
{
    /// <summary>
    /// Holds information about a service's certificate and connection rotations if applicable.
    /// </summary>
    public class ServiceRotationResult
    {
        public bool? Certificates { get; set; }
        public ConnectionInfo Connections { get; set; }

        public ServiceRotationResult()
        {
            Certificates = null;
            Connections = new ConnectionInfo();
        }

        public bool AnyFailure()
        {
            return (Certificates.HasValue && !Certificates.Value) ||
                Connections.AnyFailure();
        }

        public class ConnectionInfo
        {
            public bool? PostgreSql { get; set; } = null;
            public bool? RabbitMq { get; set; } = null;
            public bool? Redis { get; set; } = null;

            public void SetValues(bool value)
            {
                if (PostgreSql.HasValue) PostgreSql = true;
                if (RabbitMq.HasValue) RabbitMq = true;
                if (Redis.HasValue) Redis = true;
            }

            internal bool AnyFailure()
            {
                return (PostgreSql.HasValue && !PostgreSql.Value) ||
                    (RabbitMq.HasValue && !RabbitMq.Value) ||
                    (Redis.HasValue && !Redis.Value);
            }
        }
    }
}
