using System.Security.Cryptography;
using AuthService.Application.Abstractions.MessageBrokers;
using AuthService.Application.Options;
using AuthService.Persistence.DbContexts;
using Microsoft.Extensions.Options;
using Shared.Aws.Helpers.Abstractions;
using Shared.Crypto;
using Shared.RabbitMq.Helpers;
using Shared.RabbitMq.Helpers.BackgroundServices;
using Shared.RabbitMq.Helpers.EntityFramework;
using Shared.RabbitMq.Helpers.Structures;

namespace AuthService.Api.BackgroundServices
{
    public class RabbitMqPublisherBackgroundService : PublisherBackgroundService
    {
        private readonly string _awsKmsAlias;
        private readonly IKeyManagement _keyManagement;
        private readonly IServiceScopeFactory _scopeFactory;

        public RabbitMqPublisherBackgroundService(
            IOptions<ConnectionsOptions> connectionsOptions,
            IOptions<CABundleOptions> caBundleOptions,
            IOptions<EncrpytionOptions> encryptionOptions,
            IKeyManagement keyManagement,
            IServiceScopeFactory scopeFactory,
            IEmailService emailService,
            ILogger<RabbitMqPublisherBackgroundService> logger)
            : base(
            connectionsOptions.Value.RabbitMq,
            caBundleOptions.Value.Value,
            publishers: new List<Publisher>()
            {
                (Publisher)emailService
            },
            retryPublishTime: TimeSpan.FromSeconds(5),
            graceTime: TimeSpan.FromSeconds(5),
            logger)
        {
            _awsKmsAlias = encryptionOptions.Value.AwsKmsAlias;
            _keyManagement = keyManagement;
            _scopeFactory = scopeFactory;
        }

        protected override async Task SaveRejectedMessagesAsync(
            IEnumerable<Message> rejectedMessages,
            bool isShuttingDown,
            CancellationToken cancellationToken = default)
        {
            // Encrypt messages
            var encryptionKey = await _keyManagement.GenerateEncryptionKey(_awsKmsAlias);

            var wrappedDataKey = new WrappedDataKey(encryptionKey.DekEncrypted);
            foreach (var rejectedMessage in rejectedMessages)
            {
                wrappedDataKey.RejectedMessages.Add(new RejectedMessage(
                    wrappedDataKey.Id,
                    rejectedMessage.PublisherName,
                    rejectedMessage.ExchangeName,
                    rejectedMessage.RoutingKey,
                    rejectedMessage.Properties,
                    AesGcmEncryption.Encrypt(rejectedMessage.Body, encryptionKey.Dek, deleteKey: false)));
            }

            CryptographicOperations.ZeroMemory(encryptionKey.Dek);

            // Save messages to database
            await using var scope = _scopeFactory.CreateAsyncScope();
            var authRejectedMessagesDbContext = scope.ServiceProvider.GetRequiredService<AuthRejectedMessagesDbContext>();
            
            if (isShuttingDown)
            {
                await authRejectedMessagesDbContext.WrappedDataKeys.AddAsync(wrappedDataKey);
                await authRejectedMessagesDbContext.SaveChangesAsync();
            }
            else
            {
                await authRejectedMessagesDbContext.WrappedDataKeys.AddAsync(wrappedDataKey, cancellationToken);
                await authRejectedMessagesDbContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
