using Common.PostgreSql.Helpers.Options.Extensions;
using Common.RabbitMq.Helpers.Options.Extensions;

namespace Shared.Lambda.Structures
{
    /// <summary>
    /// Holds the connections options coming from AWS Secrets Manager for administrative operations.
    /// </summary>
    /// <param name="PostgreSql">Connections options for PostgreSQL</param>
    /// <param name="RabbitMq">Connections options for RabbitMQ</param>
    public record AdminConnections(
        PostgreSqlConnectionOptions PostgreSql,
        RabbitMqConnectionOptions RabbitMq
    );
}
