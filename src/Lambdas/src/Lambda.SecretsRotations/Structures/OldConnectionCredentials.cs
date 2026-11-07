namespace Lambda.SecretsRotations.Structures
{
    /// <summary>
    /// Holds necessary information to delete old users of infrastructure services.
    /// </summary>
    public class OldConnectionCredentials
    {
        public List<PostgreSqlUser> PostgreSqlUsers { get; set; } = new List<PostgreSqlUser>();
        public List<string> RabbitMqUsers { get; set; } = new List<string>();
        public List<RedisUser> RedisUsers { get; set; } = new List<RedisUser>();

        public class PostgreSqlUser
        {
            public required string Username { get; set; }
            public required string Database { get; set; }
        }

        public class RedisUser
        {
            public required string Username { get; set; }
            public required string UserGroupId { get; set; }
        }
    }
}
