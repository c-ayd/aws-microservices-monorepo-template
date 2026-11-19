namespace Shared.Lambda.Structures
{
    /// <summary>
    /// Holds necessary information to delete old credentials from the infrastructure services.
    /// </summary>
    public class OldCredentials
    {
        public List<PostgreSqlCredentials> PostgreSql { get; set; } = new List<PostgreSqlCredentials>();
        public List<string> RabbitMq { get; set; } = new List<string>();
        public List<RedisCredentials> Redis { get; set; } = new List<RedisCredentials>();

        public class PostgreSqlCredentials
        {
            public required string Username { get; set; }
            public required string Database { get; set; }
        }

        public class RedisCredentials
        {
            public required string Username { get; set; }
            public required string UserGroupId { get; set; }
        }

        public bool IsEmpty()
        {
            return PostgreSql.Count + RabbitMq.Count + Redis.Count == 0;
        }
    }
}
