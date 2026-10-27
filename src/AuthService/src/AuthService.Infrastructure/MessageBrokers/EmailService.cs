using System.Diagnostics;
using System.Text.Json;
using AuthService.Application.Abstractions.MessageBrokers;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using Shared.RabbitMq.Helpers;
using Shared.RabbitMq.Notifications.Configurations;
using Shared.RabbitMq.Notifications.Messages;

namespace AuthService.Infrastructure.MessageBrokers
{
    public class EmailService : Publisher, IEmailService
    {
        private const int _maxRetryForMessages = 3;

        public EmailService(IHostEnvironment hostEnvironment) 
            : base(hostEnvironment.ApplicationName, nameof(EmailService), _maxRetryForMessages)
        {
        }

        protected override async Task DeclareExchangesAsync(CancellationToken cancellationToken)
        {
            await Channel!.ExchangeDeclareAsync(
                EmailConfiguration.ExchangeName,
                EmailConfiguration.ExchangeType,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);
        }

        public async Task SendAsync(EmailMessage message, string? userId = null, CancellationToken cancellationToken = default)
        {
            await PublishMessageAsync(
                EmailConfiguration.ExchangeName,
                EmailConfiguration.RoutingKey,
                new BasicProperties()
                {
                    DeliveryMode = DeliveryModes.Persistent,
                    CorrelationId = Activity.Current!.TraceId.ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    Headers = new Dictionary<string, object?>
                    {
                        { "UserId", userId }
                    }
                },
                body: JsonSerializer.SerializeToUtf8Bytes(message),
                cancellationToken: cancellationToken);
        }
    }
}
