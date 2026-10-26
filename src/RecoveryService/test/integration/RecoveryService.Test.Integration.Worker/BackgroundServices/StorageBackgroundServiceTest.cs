using System.Net.Mime;
using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RecoveryService.Test.Integration.Worker.Collections;
using RecoveryService.Worker.Options;
using Shared.Aws.Helpers.Abstractions;
using Shared.Crypto;
using Shared.RabbitMq.Helpers.EntityFramework.Entities;
using Shared.RabbitMq.Helpers.Structures;
using Shared.Test.Generators;

namespace RecoveryService.Test.Integration.Worker.BackgroundServices
{
    [Collection(nameof(WorkerCollection))]
    public class StorageBackgroundServiceTest
    {
        private const int _timeoutInSeconds = 30;

        private readonly WorkerCollectionCluster _collectionCluster;

        public StorageBackgroundServiceTest(WorkerCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task RepublishAsync_WhenThereAreFiles_ShouldRepublishMessages()
        {
            // Arrange
            var queueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(WorkerCollectionCluster.QueueName);
            if (queueMessageCount != 0)
                Assert.Fail($"The queues is not empty. Count: {queueMessageCount}.");

            var awsKms = _collectionCluster.RecoveryWebApp.GetService<IAwsKms>();
            var dataKey = await awsKms.GenerateDataKeyAsync("alias/dropped-messages");

            var wrappedDataKey = new WrappedDataKey(dataKey.Encrypted);
            for (int i = 0; i < 100; ++i)
            {
                wrappedDataKey.DroppedMessages.Add(new DroppedMessage(
                    wrappedDataKey.Id,
                    serviceName: "TestService",
                    publisherName: "TestPublisher",
                    exchangeName: "test.recovery",
                    routingKey: "test.recovery.send",
                    properties: new BasicProperties()
                    {
                        DeliveryMode = DeliveryModes.Persistent,
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    bodyEncrypted: AesGcmEncryption.Encrypt(Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric()), dataKey.Plaintext, deleteKey: false)));
            }

            var awsS3Bucket = _collectionCluster.RecoveryWebApp.GetService<IAwsS3Bucket>();

            var storageOptions = _collectionCluster.RecoveryWebApp.GetOptions<StorageOptions>();
            var jsonString = JsonSerializer.Serialize(new MessageFileContent(wrappedDataKey));
            await awsS3Bucket.UploadFileAsync(
                storageOptions.DroppedMessagesBucketName,
                storageOptions.Folders[0],
                "test-file.json",
                Encoding.UTF8.GetBytes(jsonString),
                MediaTypeNames.Application.Json);
            
            // Act
            // DatabaseBackgroundService should handle it automaticaly.

            // Arrange
            var elapsedTime = 0;
            while (true)
            {
                var fileNames = await awsS3Bucket.GetFileNamesUnderFolderAsync(storageOptions.DroppedMessagesBucketName, storageOptions.Folders[0]);
                if (fileNames.Count == 0)
                    break;

                if (elapsedTime >= _timeoutInSeconds)
                    Assert.Fail($"The test waited for {_timeoutInSeconds} seconds, but the file was not deleted from the storage.");

                await Task.Delay(1000);
                ++elapsedTime;
            }

            var numberOfMessagesInRabbitMq = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(WorkerCollectionCluster.QueueName);
            Assert.Equal((uint)wrappedDataKey.DroppedMessages.Count, numberOfMessagesInRabbitMq);

            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(WorkerCollectionCluster.QueueName);
        }
    }
}
