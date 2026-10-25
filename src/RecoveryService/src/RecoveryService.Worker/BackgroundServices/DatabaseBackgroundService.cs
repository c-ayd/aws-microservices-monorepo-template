using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RecoveryService.Worker.BackgroundServices.SeedWork;
using RecoveryService.Worker.Options;
using Shared.Aws.Helpers.Abstractions;
using Shared.Crypto;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;

namespace RecoveryService.Worker.BackgroundServices
{
    public class DatabaseBackgroundService : RepublishBackgroundServiceBase
    {
        private readonly int _batchSize = 200;

        private readonly IServiceScopeFactory _scopeFactory;
        private IAwsKms _awsKms;
        private readonly ILogger<DatabaseBackgroundService> _logger;

        public DatabaseBackgroundService(
            IServiceScopeFactory scopeFactory,
            IOptions<ConnectionsOptions> connectionsOptions,
            IOptions<CABundleOptions> caBundleOptions,
            IAwsKms awsKms,
            ILogger<DatabaseBackgroundService> logger)
            : base(
            connectionsOptions.Value.RabbitMq,
            caBundleOptions.Value.Value,
            sleepTime: TimeSpan.FromSeconds(10),
            logger)
        {
            _scopeFactory = scopeFactory;
            _awsKms = awsKms;
            _logger = logger;
        }

        protected override async Task RepublishAsync(CancellationToken cancellationToken)
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            await using var dbContext = scope.ServiceProvider.GetRequiredService<DroppedMessagesDbContext>();

            await HandleDroppedMessagesInDbAsync(dbContext, cancellationToken);
        }

        private async Task HandleDroppedMessagesInDbAsync(DroppedMessagesDbContext dbContext, CancellationToken cancellationToken)
        {
            // Check if there is any dropped messages bounded to a wrapped data key. If not,
            // the background service goes to sleep for a short period of time.
            if (await dbContext.WrappedDataKeys.CountAsync() == 0)
                return;

            var wrappedDataKey = await dbContext.WrappedDataKeys
                .Include(wdk => wdk.DroppedMessages
                    .Take(_batchSize))
                .FirstAsync(cancellationToken);

            // Check if the wrapped data key has any dropped messages. If not, the key is deleted
            // from the DB and the background service goes to sleep for a short period of time.
            if (wrappedDataKey.DroppedMessages.Count == 0)
            {
                dbContext.WrappedDataKeys.Remove(wrappedDataKey);
                await dbContext.SaveChangesAsync();
                return;
            }

            var encryptionKey = await _awsKms.DecryptAsync(wrappedDataKey.Value, aad: null, cancellationToken);
            foreach (var droppedMessage in wrappedDataKey.DroppedMessages)
            {
                // To spend less time having the encryption key in the memory, the encrpytion process for the entire batch
                // needs to be done first. Once the encryption is done, the plain body is saved into the BodyEncrpyted property
                // and is used normal message body while republishing these dropped messages.
                droppedMessage.BodyEncrypted = AesGcmEncryption.Decrypt(droppedMessage.BodyEncrypted, encryptionKey, deleteKey: false);
            }
            CryptographicOperations.ZeroMemory(encryptionKey);

            var messagesToRemove = new List<Guid>();
            foreach (var droppedMessage in wrappedDataKey.DroppedMessages)
            {
                try
                {
                    await Channel!.BasicPublishAsync(
                        droppedMessage.ExchangeName,
                        droppedMessage.RoutingKey,
                        mandatory: true,
                        droppedMessage.GetBasicProperties(),
                        droppedMessage.BodyEncrypted,
                        cancellationToken);

                    messagesToRemove.Add(droppedMessage.Id);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    _logger.LogError(exception, "Something went wrong. Message: {Message}",
                        exception.Message);
                }
            }

            if (wrappedDataKey.DroppedMessages.Count == messagesToRemove.Count &&
                wrappedDataKey.DroppedMessages.Count < _batchSize)
            {
                // If all dropped messages are handled and the number of messaages is lower than the batch size,
                // this means that there is no more dropped messages under the wrapped data key. The key can be
                // deleted directly here, which causes the cascade deletion.
                dbContext.WrappedDataKeys.Remove(wrappedDataKey);
                await dbContext.SaveChangesAsync();
            }
            else
            {
                await dbContext.DroppedMessages
                    .Where(rm => messagesToRemove.Contains(rm.Id))
                    .ExecuteDeleteAsync();
            }
        }
    }
}
