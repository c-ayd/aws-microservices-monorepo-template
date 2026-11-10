namespace Lambda.SecretsRotations.Structures
{
    /// <summary>
    /// Holds necessary information to delete old users from the infrastructure services.
    /// </summary>
    public class OldConnections
    {
        public List<string> PostgreSqlRoles { get; set; } = new List<string>();
        public List<string> RabbitMqUsers { get; set; } = new List<string>();
        public List<RedisUser> RedisUsers { get; set; } = new List<RedisUser>();

        public class RedisUser
        {
            public required string Username { get; set; }
            public required string UserGroupId { get; set; }
        }
    }
}
