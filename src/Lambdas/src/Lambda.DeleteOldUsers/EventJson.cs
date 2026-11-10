namespace Lambda.DeleteOldConnections
{
    /// <summary>
    /// Holds the information about the 'detail' section of the given input.
    /// </summary>
    public class EventJson
    {
        public required string CABundleSecretId { get; set; }
        public required string AdminConnectionsSecretId { get; set; }
        public required string OldUsersSecretId { get; set; }
        public EnvironmentInfo? Environment { get; set; }

        public class EnvironmentInfo
        {
            public required bool IsProduction { get; set; }
            public string? ServiceUrl { get; set; }
        }
    }
}
