using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.MessageBrokers;
using AuthService.Application.Options;
using AuthService.Persistence.DbContexts;
using Microsoft.Extensions.Options;
using Shared.Crypto;
using Shared.RabbitMq.Helpers;
using Shared.RabbitMq.Helpers.BackgroundServices;
using Shared.RabbitMq.Helpers.EntityFramework;
using Shared.RabbitMq.Helpers.Structures;

namespace AuthService.Api.BackgroundServices
{
    public class RabbitMqPublisherBackgroundService : PublisherBackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IAesGcmEncryptionVersions _aesGcmEncryptionVersions;

        public RabbitMqPublisherBackgroundService(
            IOptions<ConnectionsOptions> connectionsOptions,
            IServiceScopeFactory scopeFactory,
            IAesGcmEncryptionVersions aesGcmEncryptionVersions,
            IEmailService emailService,
            ILogger<RabbitMqPublisherBackgroundService> logger)
            : base(
            connectionsOptions.Value.RabbitMq,
            publishers: new List<Publisher>()
            {
                (Publisher)emailService
            },
            retryPublishTime: TimeSpan.FromSeconds(5),
            graceTime: TimeSpan.FromSeconds(5),
            logger)
        {
            _scopeFactory = scopeFactory;
            _aesGcmEncryptionVersions = aesGcmEncryptionVersions;
        }

        protected override async Task SaveRejectedMessagesAsync(
            IEnumerable<Message> rejectedMessages,
            bool isShuttingDown,
            CancellationToken cancellationToken = default)
        {
            var messages = new List<RejectedMessage>();
            foreach (var rejectedMessage in rejectedMessages)
            {
                messages.Add(new RejectedMessage(
                    rejectedMessage.PublisherName,
                    rejectedMessage.ExchangeName,
                    rejectedMessage.RoutingKey,
                    rejectedMessage.Properties,
                    AesGcmEncryption.Encrypt(rejectedMessage.Body, _aesGcmEncryptionVersions.CurrentVersion, _aesGcmEncryptionVersions.GetEncryptionKey)));
            }

            await using var scope = _scopeFactory.CreateAsyncScope();
            var authRejectedMessagesDbContext = scope.ServiceProvider.GetRequiredService<AuthRejectedMessagesDbContext>();
            
            if (isShuttingDown)
            {
                await authRejectedMessagesDbContext.AddRangeAsync(messages);
                await authRejectedMessagesDbContext.SaveChangesAsync();
            }
            else
            {
                await authRejectedMessagesDbContext.AddRangeAsync(messages, cancellationToken);
                await authRejectedMessagesDbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
