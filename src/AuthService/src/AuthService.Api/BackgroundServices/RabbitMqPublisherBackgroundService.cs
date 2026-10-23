using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AuthService.Application.Abstractions.MessageBrokers;
using AuthService.Application.Options;
using AuthService.Persistence.DbContexts;
using Microsoft.Extensions.Options;
using Shared.Aws.Helpers.Abstractions;
using Shared.Crypto;
using Shared.RabbitMq.Helpers;
using Shared.RabbitMq.Helpers.BackgroundServices;
using Shared.RabbitMq.Helpers.EntityFramework.Entities;
using Shared.RabbitMq.Helpers.Structures;

namespace AuthService.Api.BackgroundServices
{
    public class RabbitMqPublisherBackgroundService : PublisherBackgroundService
    {
        private const int _minNumberOfRejectedMessagesForFallback = 1000;

        private readonly string _awsKmsAlias;
        private readonly IKeyManagement _keyManagement;
        private readonly string _awsS3BucketName;
        private readonly IStorage _storage;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<RabbitMqPublisherBackgroundService> _logger;

        public RabbitMqPublisherBackgroundService(
            IOptions<ConnectionsOptions> connectionsOptions,
            IOptions<CABundleOptions> caBundleOptions,
            IOptions<EncrpytionOptions> encryptionOptions,
            IOptions<StorageOptions> storageOptions,
            IKeyManagement keyManagement,
            IStorage storage,
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
            _awsS3BucketName = storageOptions.Value.AwsS3BucketName;
            _storage = storage;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task<bool> SaveRejectedMessagesAsync(
            IEnumerable<Message> rejectedMessages,
            int messageCount,
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
                    rejectedMessage.ExchangeName,
                    rejectedMessage.RoutingKey,
                    rejectedMessage.Properties,
                    AesGcmEncryption.Encrypt(rejectedMessage.Body, encryptionKey.Dek, deleteKey: false)));
            }

            CryptographicOperations.ZeroMemory(encryptionKey.Dek);

            // Save messages to database
            await using var scope = _scopeFactory.CreateAsyncScope();
            var authRejectedMessagesDbContext = scope.ServiceProvider.GetRequiredService<AuthRejectedMessagesDbContext>();

            try
            {
                await authRejectedMessagesDbContext.WrappedDataKeys.AddAsync(wrappedDataKey, cancellationToken);
                await authRejectedMessagesDbContext.SaveChangesAsync(cancellationToken);

                return true;
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("The saving rejected messages operation is cancelled.");

                // Since the operation is cancelled, it should not even check anything later.
                return false;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Something went wrong. Message: {Message}, Shutting Down: {ShuttingDown}",
                    exception.Message,
                    isShuttingDown);
            }

            // S3 Bucket is used as the fallback mechanism. Even if the saving messages to the database process fails,
            // if the application is not shutting down then there will be another chance to save these messages to the DB.
            // The only exception is when the rejected messages count is too large. In this case, the rejected messages are
            // saved to the related S3 Bucket regardless of the application state.
            if (!isShuttingDown && messageCount < _minNumberOfRejectedMessagesForFallback)
                return false;

            var jsonString = JsonSerializer.Serialize(new MessageFileContent(wrappedDataKey));
            await _storage.UploadFileAsync(
                _awsS3BucketName,
                folder: "auth-svc",
                fileName: $"{DateTimeOffset.UtcNow.ToString("yyyy-MM-dd_HH:mm:ss")}_{Guid.NewGuid()}.json",
                content: Encoding.UTF8.GetBytes(jsonString),
                contentType: MediaTypeNames.Application.Json);
            
            return true;
        }
    }
}
