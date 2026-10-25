using System.Text;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RecoveryService.Test.Integration.Worker.Collections;
using Shared.Aws.Helpers.Abstractions;
using Shared.Crypto;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;
using Shared.RabbitMq.Helpers.EntityFramework.Entities;

namespace RecoveryService.Test.Integration.Worker.BackgroundServices
{
    [Collection(nameof(WorkerCollection))]
    public class DatabaseBackgroundServiceTest
    {
        private const int _timeoutInSeconds = 30;

        private readonly WorkerCollectionCluster _collectionCluster;

        public DatabaseBackgroundServiceTest(WorkerCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task RepublishAsync_WhenThereIsKeyInDbButHasNoMessages_ShouldDeleteKey()
        {
            // Arrange
            await using var dbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<DroppedMessagesDbContext>();

            dbContext.WrappedDataKeys.Add(new WrappedDataKey([1, 2, 3, 4, 5]));
            await dbContext.SaveChangesAsync();

            // Act
            // DatabaseBackgroundService should handle it automaticaly.

            // Arrange
            dbContext.ChangeTracker.Clear();

            var elapsedTime = 0;
            while (true)
            {
                if (await dbContext.WrappedDataKeys.CountAsync() == 0)
                    break;

                if (elapsedTime >= _timeoutInSeconds)
                    Assert.Fail($"The test waited for {_timeoutInSeconds} seconds, but the key was not deleted from the DB.");

                await Task.Delay(1000);
                ++elapsedTime;
            }

            await _collectionCluster.PostgreSqlFixture.ClearDatabaseAsync<DroppedMessagesDbContext>();
        }

        [Theory]
        [InlineData(100)]
        [InlineData(200)]
        public async Task RepublishAsync_WhenThereAreKeyAndMessages_ShouldRepublishMessages(int numberOfMessages)
        {
            // Arrange
            var queueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(WorkerCollectionCluster.QueueName);
            if (queueMessageCount != 0)
                Assert.Fail($"The queues is not empty. Count: {queueMessageCount}.");
            
            var awsKms = _collectionCluster.RecoveryWebApp.GetService<IKeyManagement>();
            var encryptionKey = await awsKms.GenerateEncryptionKeyAsync("alias/test-alias");
            
            var wrappedDataKey = new WrappedDataKey(encryptionKey.DekEncrypted);
            var message = "Test message";
            for (int i = 0; i < numberOfMessages; ++i)
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
                    bodyEncrypted: AesGcmEncryption.Encrypt(Encoding.UTF8.GetBytes(message), encryptionKey.Dek, deleteKey: false)));
            }

            await using var dbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<DroppedMessagesDbContext>();

            dbContext.WrappedDataKeys.Add(wrappedDataKey);
            await dbContext.SaveChangesAsync();

            // Act
            // DatabaseBackgroundService should handle it automaticaly.

            // Arrange
            dbContext.ChangeTracker.Clear();

            var elapsedTime = 0;
            while (true)
            {
                if (await dbContext.WrappedDataKeys.CountAsync() == 0)
                    break;

                if (elapsedTime >= _timeoutInSeconds)
                    Assert.Fail($"The test waited for {_timeoutInSeconds} seconds, but the key was not deleted from the DB.");

                await Task.Delay(1000);
                ++elapsedTime;
            }

            await _collectionCluster.PostgreSqlFixture.ClearDatabaseAsync<DroppedMessagesDbContext>();

            var numberOfMessagesInRabbitMq = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(WorkerCollectionCluster.QueueName);
            Assert.Equal((uint)numberOfMessages, numberOfMessagesInRabbitMq);

            var messageFromRabbit = await _collectionCluster.RabbitMqFixture.GetNextMessageAsync(WorkerCollectionCluster.QueueName);
            Assert.Equal(message, Encoding.UTF8.GetString(messageFromRabbit!.Body.ToArray()));

            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(WorkerCollectionCluster.QueueName);
        }
    }
}
