namespace Lambda.DeleteOldConnections.Structures
{
    /// <summary>
    /// Holds information about whether users are deleted successfully or not.
    /// </summary>
    public class DeletionResults
    {
        public List<string> DeletedPostgreSqlUsers { get; set; } = new List<string>();
        public List<string> FailedPostgreSqlUsers { get; set; } = new List<string>();
        public List<string> DeletedRabbitMqUsers { get; set; } = new List<string>();
        public List<string> FailedRabbitMqUsers { get; set; } = new List<string>();
        public List<string> DeletedRedisUsers { get; set; } = new List<string>();
        public List<string> FailedRedisUsers { get; set; } = new List<string>();
    }
}
