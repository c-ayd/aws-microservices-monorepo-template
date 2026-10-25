using System.Reflection;
using System.Text.Json;
using AuthService.Api.BackgroundServices;
using AuthService.Test.Integration.Api.Collections;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using Shared.Aws.Helpers.Abstractions;
using Shared.Crypto;
using Shared.RabbitMq.Helpers.EntityFramework.DbContexts;
using Shared.RabbitMq.Helpers.Structures;
using Shared.Test.Generators;

namespace AuthService.Test.Integration.Api.BackgroundServices
{
    [Collection(nameof(AuthApiCollection))]
    public class RabbitMqPublisherBackgroundServiceTest
    {
        private readonly AuthApiCollectionCluster _collectionCluster;

        public RabbitMqPublisherBackgroundServiceTest(AuthApiCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task SaveDroppedMessagesAsync_WhenThereAreMessagesAndNoExceptionIsThrown_ShouldSaveMessageInDb(bool isShuttingDown)
        {
            // Arrange
            var saveDroppedMessagesMethodInfo = typeof(RabbitMqPublisherBackgroundService).GetMethod("SaveDroppedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var backgroundService = _collectionCluster.AuthApiWebApp.GetBackgroundService<RabbitMqPublisherBackgroundService>();

            var rejectedMessages = new List<Message>();
            for (int i = 0; i < 2; ++i)
            {
                rejectedMessages.Add(new Message(
                    publisherName: StringGenerator.GenerateAlphanumeric(),
                    exchangeName: StringGenerator.GenerateAlphanumeric(),
                    routingKey: StringGenerator.GenerateAlphanumeric(),
                    properties: new BasicProperties()
                    {
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    body: JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GenerateAlphanumeric())));
            }

            // Act
            await (Task)saveDroppedMessagesMethodInfo.Invoke(backgroundService, [rejectedMessages, rejectedMessages.Count, isShuttingDown, default])!;

            // Assert
            await using var rejectedMessagesDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<RejectedMessagesDbContext>();
            var messagesFromDb = await rejectedMessagesDbContext.RejectedMessages
                .Include(rm => rm.WrappedDataKey)
                .ToListAsync();

            var encryptionKey = await _collectionCluster.AuthApiWebApp.GetService<IKeyManagement>().DecryptAsync(messagesFromDb[0].WrappedDataKey.Value);
            foreach (var rejectedMessage in rejectedMessages)
            {
                var message = messagesFromDb.FirstOrDefault(m => m.GetBasicProperties().CorrelationId == rejectedMessage.Properties.CorrelationId);
                if (message == null)
                    Assert.Fail("One of the messages is not saved to the DB.");

                var messageBodyDecrypted = AesGcmEncryption.Decrypt(message.BodyEncrypted, encryptionKey, deleteKey: false);
                Assert.Equal(rejectedMessage.Body, messageBodyDecrypted);
            }

            rejectedMessagesDbContext.RejectedMessages.RemoveRange(messagesFromDb);
            await rejectedMessagesDbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task SaveDroppedMessagesAsync_WhenAppIsNotShuttinDownAndThereAreMessagesAndExceptionIsThrown_ShouldDoNothing()
        {
            // Arrange
            var saveDroppedMessagesMethodInfo = typeof(RabbitMqPublisherBackgroundService).GetMethod("SaveDroppedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var backgroundService = _collectionCluster.AuthApiWebApp.GetBackgroundService<RabbitMqPublisherBackgroundService>();

            var rejectedMessages = new List<Message>();
            for (int i = 0; i < 2; ++i)
            {
                rejectedMessages.Add(new Message(
                    publisherName: StringGenerator.GenerateAlphanumeric(),
                    exchangeName: StringGenerator.GenerateAlphanumeric(),
                    routingKey: StringGenerator.GenerateAlphanumeric(),
                    properties: new BasicProperties()
                    {
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    body: JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GenerateAlphanumeric())));
            }

            await _collectionCluster.PostgreSqlFixture.BlockTrafficAsync();

            // Act
            await (Task)saveDroppedMessagesMethodInfo.Invoke(backgroundService, [rejectedMessages, rejectedMessages.Count, false, default])!;

            // Assert
            await _collectionCluster.PostgreSqlFixture.UnblockTrafficAsync();

            await using var rejectedMessagesDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<RejectedMessagesDbContext>();
            var messagesFromDb = await rejectedMessagesDbContext.RejectedMessages
                .Include(rm => rm.WrappedDataKey)
                .ToListAsync();

            int counter = 0;
            foreach (var rejectedMessage in rejectedMessages)
            {
                var message = messagesFromDb.FirstOrDefault(m => m.GetBasicProperties().CorrelationId == rejectedMessage.Properties.CorrelationId);
                if (message != null)
                {
                    ++counter;
                }
            }
            Assert.Equal(0, counter);
        }

        [Fact]
        public async Task SaveDroppedMessagesAsync_WhenAppIsNotShuttinDownAndMessagesExceedMinBatchSizeAndExceptionIsThrown_ShouldSaveMessagesToStorage()
        {
            // Arrange
            var saveDroppedMessagesMethodInfo = typeof(RabbitMqPublisherBackgroundService).GetMethod("SaveDroppedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var backgroundService = _collectionCluster.AuthApiWebApp.GetBackgroundService<RabbitMqPublisherBackgroundService>();

            var rejectedMessages = new List<Message>();
            var minBatchSize = (int)typeof(RabbitMqPublisherBackgroundService).GetField("_minNumberOfRejectedMessagesForFallback", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
            for (int i = 0; i < minBatchSize; ++i)
            {
                rejectedMessages.Add(new Message(
                    publisherName: StringGenerator.GenerateAlphanumeric(),
                    exchangeName: StringGenerator.GenerateAlphanumeric(),
                    routingKey: StringGenerator.GenerateAlphanumeric(),
                    properties: new BasicProperties()
                    {
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    body: JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GenerateAlphanumeric())));
            }

            await _collectionCluster.PostgreSqlFixture.BlockTrafficAsync();

            // Act
            await (Task)saveDroppedMessagesMethodInfo.Invoke(backgroundService, [rejectedMessages, rejectedMessages.Count, false, default])!;

            // Assert
            await _collectionCluster.PostgreSqlFixture.UnblockTrafficAsync();

            var awsS3Bucket = _collectionCluster.AuthApiWebApp.GetService<IStorage>();
            var fileNames = await awsS3Bucket.GetFileNamesUnderFolderAsync("template-graceful-shutdown", "auth-svc");
            Assert.Single(fileNames);
            
            await awsS3Bucket.DeleteFilesAsync("template-graceful-shutdown", "auth-svc", fileNames);
        }

        [Fact]
        public async Task SaveDroppedMessagesAsync_WhenAppIsShuttinDownAndThereAreMessagesAndExceptionIsThrown_ShouldSaveMessagesToStorage()
        {
            // Arrange
            var saveDroppedMessagesMethodInfo = typeof(RabbitMqPublisherBackgroundService).GetMethod("SaveDroppedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var backgroundService = _collectionCluster.AuthApiWebApp.GetBackgroundService<RabbitMqPublisherBackgroundService>();

            var rejectedMessages = new List<Message>();
            for (int i = 0; i < 2; ++i)
            {
                rejectedMessages.Add(new Message(
                    publisherName: StringGenerator.GenerateAlphanumeric(),
                    exchangeName: StringGenerator.GenerateAlphanumeric(),
                    routingKey: StringGenerator.GenerateAlphanumeric(),
                    properties: new BasicProperties()
                    {
                        CorrelationId = Guid.NewGuid().ToString()
                    },
                    body: JsonSerializer.SerializeToUtf8Bytes(StringGenerator.GenerateAlphanumeric())));
            }

            await _collectionCluster.PostgreSqlFixture.BlockTrafficAsync();

            // Act
            await (Task)saveDroppedMessagesMethodInfo.Invoke(backgroundService, [rejectedMessages, rejectedMessages.Count, true, default])!;

            // Assert
            await _collectionCluster.PostgreSqlFixture.UnblockTrafficAsync();

            var awsS3Bucket = _collectionCluster.AuthApiWebApp.GetService<IStorage>();
            var fileNames = await awsS3Bucket.GetFileNamesUnderFolderAsync("template-graceful-shutdown", "auth-svc");
            Assert.Single(fileNames);

            await awsS3Bucket.DeleteFilesAsync("template-graceful-shutdown", "auth-svc", fileNames);
        }
    }
}
