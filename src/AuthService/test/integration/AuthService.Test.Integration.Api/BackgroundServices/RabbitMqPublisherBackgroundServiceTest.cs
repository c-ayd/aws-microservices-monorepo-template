using System.Reflection;
using System.Text;
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
        private const string _folderNameForDroppedMessages = "auth-svc";

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
            var saveDroppedMessagesMethodInfo = typeof(RabbitMqPublisherBackgroundService)
                .GetMethod("SaveDroppedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var backgroundService = _collectionCluster.AuthApiWebApp.GetBackgroundService<RabbitMqPublisherBackgroundService>();

            var droppedMessages = new List<Message>();
            for (int i = 0; i < 2; ++i)
            {
                droppedMessages.Add(new Message(
                    serviceName: StringGenerator.GenerateAlphanumeric(),
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
            await (Task)saveDroppedMessagesMethodInfo.Invoke(backgroundService, [droppedMessages, droppedMessages.Count, isShuttingDown, default])!;

            // Assert
            await using var droppedMessagesDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<DroppedMessagesDbContext>();
            var messagesFromDb = await droppedMessagesDbContext.DroppedMessages
                .Include(rm => rm.WrappedDataKey)
                .ToListAsync();

            var dataKeyPlaintext = await _collectionCluster.AuthApiWebApp.GetService<IAwsKms>()
                .DecryptAsync(messagesFromDb[0].WrappedDataKey.Value);
            foreach (var droppedMessage in droppedMessages)
            {
                var message = messagesFromDb.FirstOrDefault(m => m.GetBasicProperties().CorrelationId == droppedMessage.Properties.CorrelationId);
                if (message == null)
                    Assert.Fail("One of the messages is not saved to the DB.");

                var messageBodyDecrypted = AesGcmEncryption.Decrypt(message.BodyEncrypted, dataKeyPlaintext, deleteKey: false);
                Assert.Equal(droppedMessage.Body, messageBodyDecrypted);
            }

            droppedMessagesDbContext.DroppedMessages.RemoveRange(messagesFromDb);
            await droppedMessagesDbContext.SaveChangesAsync();
        }

        [Fact]
        public async Task SaveDroppedMessagesAsync_WhenAppIsNotShuttinDownAndThereAreMessagesAndExceptionIsThrown_ShouldDoNothing()
        {
            // Arrange
            var saveDroppedMessagesMethodInfo = typeof(RabbitMqPublisherBackgroundService)
                .GetMethod("SaveDroppedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var backgroundService = _collectionCluster.AuthApiWebApp.GetBackgroundService<RabbitMqPublisherBackgroundService>();

            var droppedMessages = new List<Message>();
            for (int i = 0; i < 2; ++i)
            {
                droppedMessages.Add(new Message(
                    serviceName: StringGenerator.GenerateAlphanumeric(),
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
            try
            {
                await (Task)saveDroppedMessagesMethodInfo.Invoke(backgroundService, [droppedMessages, droppedMessages.Count, false, default])!;
            }
            finally
            {
                await _collectionCluster.PostgreSqlFixture.UnblockTrafficAsync();
            }
            
            // Assert
            await using var droppedMessagesDbContext = _collectionCluster.PostgreSqlFixture.CreateDbContext<DroppedMessagesDbContext>();
            var messagesFromDb = await droppedMessagesDbContext.DroppedMessages
                .Include(rm => rm.WrappedDataKey)
                .ToListAsync();

            int counter = 0;
            foreach (var droppedMessage in droppedMessages)
            {
                var message = messagesFromDb.FirstOrDefault(m => m.GetBasicProperties().CorrelationId == droppedMessage.Properties.CorrelationId);
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
            var saveDroppedMessagesMethodInfo = typeof(RabbitMqPublisherBackgroundService)
                .GetMethod("SaveDroppedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var backgroundService = _collectionCluster.AuthApiWebApp.GetBackgroundService<RabbitMqPublisherBackgroundService>();

            var droppedMessages = new List<Message>();
            var minBatchSize = (int)typeof(RabbitMqPublisherBackgroundService)
                .GetField("_minNumberOfDroppedMessagesForFallback", BindingFlags.NonPublic | BindingFlags.Static)!
                .GetValue(null)!;
            for (int i = 0; i < minBatchSize; ++i)
            {
                droppedMessages.Add(new Message(
                    serviceName: StringGenerator.GenerateAlphanumeric(),
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
            try
            {
                await (Task)saveDroppedMessagesMethodInfo.Invoke(backgroundService, [droppedMessages, droppedMessages.Count, false, default])!;
            }
            finally
            {
                await _collectionCluster.PostgreSqlFixture.UnblockTrafficAsync();
            }

            // Assert
            var awsS3Bucket = _collectionCluster.AuthApiWebApp.GetService<IAwsS3Bucket>();
            var fileNames = await awsS3Bucket.GetFileNamesUnderFolderAsync(
                AuthApiCollectionCluster.AwsS3BucketNameForDroppedMessages,
                _folderNameForDroppedMessages);
            Assert.Single(fileNames);

            await using var fileContent = await awsS3Bucket.DownloadFileAsync(
                AuthApiCollectionCluster.AwsS3BucketNameForDroppedMessages,
                _folderNameForDroppedMessages,
                fileNames[0]);
            var messageFileContent = JsonSerializer.Deserialize<MessageFileContent>(Encoding.UTF8.GetString(fileContent.ToArray()))!;

            var dataKeyPlaintext = await _collectionCluster.AuthApiWebApp.GetService<IAwsKms>()
                .DecryptAsync(messageFileContent.WrappedDataKey);
            foreach (var droppedMessage in droppedMessages)
            {
                var message = messageFileContent.Messages.FirstOrDefault(m => m.GetBasicProperties().CorrelationId == droppedMessage.Properties.CorrelationId);
                if (message == null)
                    Assert.Fail("One of the messages is not saved to the file.");

                var messageBodyDecrypted = AesGcmEncryption.Decrypt(message.BodyEncrypted, dataKeyPlaintext, deleteKey: false);
                Assert.Equal(droppedMessage.Body, messageBodyDecrypted);
            }

            await awsS3Bucket.DeleteFilesAsync("template-dropped-messages", "auth-svc", fileNames);
        }

        [Fact]
        public async Task SaveDroppedMessagesAsync_WhenAppIsShuttinDownAndThereAreMessagesAndExceptionIsThrown_ShouldSaveMessagesToStorage()
        {
            // Arrange
            var saveDroppedMessagesMethodInfo = typeof(RabbitMqPublisherBackgroundService)
                .GetMethod("SaveDroppedMessagesAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var backgroundService = _collectionCluster.AuthApiWebApp.GetBackgroundService<RabbitMqPublisherBackgroundService>();

            var droppedMessages = new List<Message>();
            for (int i = 0; i < 2; ++i)
            {
                droppedMessages.Add(new Message(
                    serviceName: StringGenerator.GenerateAlphanumeric(),
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
            try
            {
                await (Task)saveDroppedMessagesMethodInfo.Invoke(backgroundService, [droppedMessages, droppedMessages.Count, true, default])!;
            }
            finally
            {
                await _collectionCluster.PostgreSqlFixture.UnblockTrafficAsync();
            }

            // Assert
            var awsS3Bucket = _collectionCluster.AuthApiWebApp.GetService<IAwsS3Bucket>();
            var fileNames = await awsS3Bucket.GetFileNamesUnderFolderAsync(
                AuthApiCollectionCluster.AwsS3BucketNameForDroppedMessages,
                _folderNameForDroppedMessages);
            Assert.Single(fileNames);

            await using var fileContent = await awsS3Bucket.DownloadFileAsync(
                AuthApiCollectionCluster.AwsS3BucketNameForDroppedMessages,
                _folderNameForDroppedMessages,
                fileNames[0]);
            var messageFileContent = JsonSerializer.Deserialize<MessageFileContent>(Encoding.UTF8.GetString(fileContent.ToArray()))!;

            var dataKeyPlaintext = await _collectionCluster.AuthApiWebApp.GetService<IAwsKms>()
                .DecryptAsync(messageFileContent.WrappedDataKey);
            foreach (var droppedMessage in droppedMessages)
            {
                var message = messageFileContent.Messages.FirstOrDefault(m => m.GetBasicProperties().CorrelationId == droppedMessage.Properties.CorrelationId);
                if (message == null)
                    Assert.Fail("One of the messages is not saved to the file.");

                var messageBodyDecrypted = AesGcmEncryption.Decrypt(message.BodyEncrypted, dataKeyPlaintext, deleteKey: false);
                Assert.Equal(droppedMessage.Body, messageBodyDecrypted);
            }

            await awsS3Bucket.DeleteFilesAsync(
                AuthApiCollectionCluster.AwsS3BucketNameForDroppedMessages,
                _folderNameForDroppedMessages,
                fileNames);
        }
    }
}
