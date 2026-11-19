namespace Lambda.DeleteOldConnections.Structures
{
    /// <summary>
    /// Holds information about whether credentials are deleted successfully or not.
    /// </summary>
    public class DeletionResults
    {
        public List<string> DeletedPostgreSqlCredentials { get; set; } = new List<string>();
        public List<string> FailedPostgreSqlCredentials { get; set; } = new List<string>();
        public List<string> DeletedRabbitMqCredentials { get; set; } = new List<string>();
        public List<string> FailedRabbitMqCredentials { get; set; } = new List<string>();
        public List<string> DeletedRedisCredentials { get; set; } = new List<string>();
        public List<string> FailedRedisCredentials { get; set; } = new List<string>();
    }
}
