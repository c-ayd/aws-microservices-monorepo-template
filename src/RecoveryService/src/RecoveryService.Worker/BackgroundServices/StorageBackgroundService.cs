using System.Net.Mime;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RecoveryService.Worker.BackgroundServices.SeedWork;
using RecoveryService.Worker.Options;
using Common.Aws.Helpers.Abstractions;
using Common.Crypto;
using Common.RabbitMq.Helpers.Structures;

namespace RecoveryService.Worker.BackgroundServices
{
    public class StorageBackgroundService : RepublishBackgroundServiceBase
    {
        private readonly StorageOptions _storageOptions;
        private readonly IAwsS3Bucket _awsS3Bucket;
        private readonly IAwsKms _awsKms;
        private readonly ILogger<StorageBackgroundService> _logger;

        public StorageBackgroundService(
            IOptions<ConnectionsOptions> connectionsOptions,
            IOptions<CABundleOptions> caBundleOptions,
            IOptions<StorageOptions> storageOptions,
            IAwsS3Bucket awsS3Bucket,
            IAwsKms awsKms,
            ILogger<StorageBackgroundService> logger)
            : base(
            connectionsOptions.Value.RabbitMq,
            caBundleOptions.Value.Value,
            sleepTime: TimeSpan.FromSeconds(10),
            logger)
        {
            _storageOptions = storageOptions.Value;
            _awsS3Bucket = awsS3Bucket;
            _awsKms = awsKms;
            _logger = logger;
        }

        protected override async Task RepublishAsync(CancellationToken cancellationToken)
        {
            foreach (var folder in _storageOptions.Folders)
            {
                await RepublishDroppedMessagesInStorageAsync(_storageOptions.DroppedMessagesBucketName, folder, cancellationToken);
            }
        }

        private async Task RepublishDroppedMessagesInStorageAsync(
            string bucketName,
            string folder,
            CancellationToken cancellationToken)
        {
            var fileNames = await _awsS3Bucket.GetFileNamesUnderFolderAsync(bucketName, folder, cancellationToken);
            if (fileNames.Count == 0)
                return;

            foreach (var fileName in fileNames)
            {
                await using var fileContent = await _awsS3Bucket.DownloadFileAsync(bucketName, folder, fileName, cancellationToken);
                var messageFileContent = JsonSerializer.Deserialize<MessageFileContent>(Encoding.UTF8.GetString(fileContent.ToArray()))!;

                var dataKeyPlaintext = await _awsKms.DecryptAsync(messageFileContent.WrappedDataKey, aad: null, cancellationToken);
                foreach (var droppedMessage in messageFileContent.Messages)
                {
                    // To spend less time having the encryption key in the memory, the encrpytion process for the entire batch
                    // needs to be done first. Once the encryption is done, the plain body is saved into the BodyEncrpyted property
                    // and is used normal message body while republishing these dropped messages.
                    droppedMessage.BodyEncrypted = AesGcmEncryption.Decrypt(droppedMessage.BodyEncrypted, dataKeyPlaintext, deleteKey: false);
                }
                CryptographicOperations.ZeroMemory(dataKeyPlaintext);

                var messagesToRemove = new List<string>();
                foreach (var droppedMessage in messageFileContent.Messages)
                {
                    try
                    {
                        var properties = droppedMessage.GetBasicProperties();
                        await Channel!.BasicPublishAsync(
                            droppedMessage.ExchangeName,
                            droppedMessage.RoutingKey,
                            mandatory: true,
                            properties,
                            droppedMessage.BodyEncrypted,
                            cancellationToken);

                        messagesToRemove.Add(properties.CorrelationId!);
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

                if (messageFileContent.Messages.Count == messagesToRemove.Count)
                {
                    await _awsS3Bucket.DeleteFileAsync(bucketName, folder, fileName);
                }
                else
                {
                    // If some messages could not be republished, the remaining messages are saved
                    // to the same file while overriding the file. So that, they can be processed later.
                    messageFileContent.Messages = messageFileContent.Messages
                        .Where(m => !messagesToRemove.Contains(m.GetBasicProperties().CorrelationId!))
                        .ToList();

                    var jsonString = JsonSerializer.Serialize(messageFileContent);
                    await _awsS3Bucket.UploadFileAsync(
                        bucketName,
                        folder,
                        fileName,
                        content: Encoding.UTF8.GetBytes(jsonString),
                        contentType: MediaTypeNames.Application.Json);
                }
            }
        }
    }
}
