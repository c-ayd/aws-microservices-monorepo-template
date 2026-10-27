using System.Text.Json;
using Microsoft.Extensions.Options;
using NotificationService.Worker.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Shared.Aws.Helpers.Abstractions;
using Shared.RabbitMq.Helpers.BackgroundServices;
using Shared.RabbitMq.Notifications.Configurations;
using Shared.RabbitMq.Notifications.Messages;

namespace NotificationService.Worker.BackgroundServices
{
    public class EmailBackgroundService : ConsumerBackgroundService
    {
        private readonly TimeSpan _retryDelayTime = TimeSpan.FromMinutes(1);
        private const int _maxRetry = 5;

        private readonly SmtpOptions _smtpOptions;
        private readonly IAwsSes _awsSes;
        private readonly ILogger<EmailBackgroundService> _logger;

        public EmailBackgroundService(
            IOptions<ConnectionsOptions> connectionsOptions,
            IOptions<CABundleOptions> caBundleOptions,
            IOptions<SmtpOptions> smtpOptions,
            IAwsSes awsSes,
            ILogger<EmailBackgroundService> logger)
            : base(
            connectionsOptions.Value.RabbitMq,
            caBundleOptions.Value.Value,
            healthCheckTime: TimeSpan.FromSeconds(10),
            graceTime: TimeSpan.FromSeconds(5),
            queueName: EmailConfiguration.QueueName,
            prefetchCount: 1,
            logger)
        {
            _smtpOptions = smtpOptions.Value;
            _awsSes = awsSes;
            _logger = logger;
        }

        protected override async Task DeclareExchangesAsync(CancellationToken cancellationToken)
        {
            await Channel!.ExchangeDeclareAsync(
                EmailConfiguration.ExchangeName,
                EmailConfiguration.ExchangeType,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await Channel.ExchangeDeclareAsync(
                EmailConfiguration.RetryExchangeName,
                EmailConfiguration.RetryExchangeType,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await Channel.ExchangeDeclareAsync(
                EmailConfiguration.DlxName,
                EmailConfiguration.DlxExchangeType,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }

        protected override async Task DeclareQueuesAsync(CancellationToken cancellationToken)
        {
            await Channel!.QueueDeclareAsync(
                EmailConfiguration.DlqName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                cancellationToken: cancellationToken);
            await Channel.QueueBindAsync(
                EmailConfiguration.DlqName,
                EmailConfiguration.DlxName,
                EmailConfiguration.DeadLetterRoutingKey,
                cancellationToken: cancellationToken);

            await Channel.QueueDeclareAsync(
                EmailConfiguration.RetryQueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    { "x-dead-letter-exchange", EmailConfiguration.ExchangeName },
                    { "x-dead-letter-routing-key", EmailConfiguration.RoutingKey }
                },
                cancellationToken: cancellationToken);
            await Channel.QueueBindAsync(
                EmailConfiguration.RetryQueueName,
                EmailConfiguration.RetryExchangeName,
                EmailConfiguration.RetryRoutingKey,
                cancellationToken: cancellationToken);

            await Channel.QueueDeclareAsync(
                EmailConfiguration.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: new Dictionary<string, object?>()
                {
                    { "x-queue-type", "quorum" },
                    { "x-dead-letter-exchange", EmailConfiguration.DlxName },
                    { "x-dead-letter-routing-key", EmailConfiguration.DeadLetterRoutingKey }
                },
                cancellationToken: cancellationToken);
            await Channel.QueueBindAsync(
                EmailConfiguration.QueueName,
                EmailConfiguration.ExchangeName,
                EmailConfiguration.RoutingKey,
                cancellationToken: cancellationToken);
        }

        protected override async Task ReceivedAsync(object obj, BasicDeliverEventArgs args)
        {
            // Deserialize message
            EmailMessage? message;
            try
            {
                message = JsonSerializer.Deserialize<EmailMessage>(args.Body.ToArray(), new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });

                if (message == null)
                {
                    _logger.LogError("The message could not be deserialized. Correlation ID: {CorrelationId}, Timestamp: {Timestamp}, User ID: {UserId}",
                        args.BasicProperties.CorrelationId,
                        args.BasicProperties.Timestamp,
                        args.BasicProperties.Headers != null && args.BasicProperties.Headers.TryGetValue("UserId", out var userId) ?
                            (string?)userId : null);

                    await Channel!.BasicRejectAsync(args.DeliveryTag, requeue: false);
                    return;
                }
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong while deserializing the message. Correlation ID: {CorrelationId}, Timestamp: {Timestamp}, User ID: {UserId}, Message: {Message}",
                    args.BasicProperties.CorrelationId,
                    args.BasicProperties.Timestamp,
                    args.BasicProperties.Headers != null && args.BasicProperties.Headers.TryGetValue("UserId", out var userId) ?
                        (string?)userId : null,
                    exception.Message);

                await Channel!.BasicRejectAsync(args.DeliveryTag, requeue: false);
                return;
            }

            // Send email
            try
            {
                await _awsSes.SendEmailAsync(
                    _smtpOptions.FromEmailAddress,
                    message.To,
                    templateName: $"{message.TemplateId}-{message.Language}",
                    message.Parameters);

                await Channel!.BasicAckAsync(args.DeliveryTag, multiple: false);
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong while sending the email. Correlation ID: {CorrelationId}, Timestamp: {Timestamp}, User ID: {UserId}, Message: {Message}",
                    args.BasicProperties.CorrelationId,
                    args.BasicProperties.Timestamp,
                    args.BasicProperties.Headers != null && args.BasicProperties.Headers.TryGetValue("UserId", out var userId) ?
                        (string?)userId : null,
                    exception.Message);

                // Since the email service has failed, this message should be requeued after a small delay up to a specific max. retry count
                if (args.BasicProperties.Headers == null || !args.BasicProperties.Headers.TryGetValue("Retry-Count", out var retryCount))
                {
                    var properties = new BasicProperties(args.BasicProperties);
                    if (properties.Headers == null)
                    {
                        properties.Headers = new Dictionary<string, object?>();
                    }
                    properties.Headers.TryAdd("Retry-Count", 1);
                    properties.Expiration = ((int)_retryDelayTime.TotalMilliseconds).ToString();

                    await Channel!.BasicPublishAsync(
                        EmailConfiguration.RetryExchangeName,
                        EmailConfiguration.RetryRoutingKey,
                        mandatory: true,
                        properties,
                        args.Body);

                    await Channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                }
                else
                {
                    int retryCountValue = (int)retryCount!;
                    if (retryCountValue >= _maxRetry)
                    {
                        // The message has been requeued too many times. Thefore, the message should go to the DLQ.
                        await Channel!.BasicRejectAsync(args.DeliveryTag, requeue: false);
                    }
                    else
                    {
                        retryCountValue = retryCountValue + 1;

                        var properties = new BasicProperties(args.BasicProperties);
                        properties.Headers!["Retry-Count"] = retryCountValue;
                        properties.Expiration = ((int)_retryDelayTime.TotalMilliseconds * retryCountValue).ToString();

                        await Channel!.BasicPublishAsync(
                            EmailConfiguration.RetryExchangeName,
                            EmailConfiguration.RetryRoutingKey,
                            mandatory: true,
                            properties,
                            args.Body);

                        await Channel.BasicAckAsync(args.DeliveryTag, multiple: false);
                    }
                }
            }
        }
    }
}
