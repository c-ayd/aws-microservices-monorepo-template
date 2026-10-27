namespace Shared.RabbitMq.Notifications.Configurations
{
    /// <summary>
    /// Holds information about the exchanges, queues and routing keys related to the sending email process.
    /// </summary>
    public static class EmailConfiguration
    {
        public const string ExchangeName = "notification.email";
        public const string ExchangeType = RabbitMQ.Client.ExchangeType.Topic;
        public const string RoutingKey = "notification.email.send";
        public const string QueueName = "notification.email";

        public const string RetryExchangeName = "notification.email.retry";
        public const string RetryExchangeType = RabbitMQ.Client.ExchangeType.Direct;
        public const string RetryRoutingKey = "notification.email.retry";
        public const string RetryQueueName = "notification.email.retry";

        public const string DlxName = "notification.email.dlx";
        public const string DlxExchangeType = RabbitMQ.Client.ExchangeType.Direct;
        public const string DeadLetterRoutingKey = "notification.email.dead";
        public const string DlqName = "notification.email.dlq";
    }
}
