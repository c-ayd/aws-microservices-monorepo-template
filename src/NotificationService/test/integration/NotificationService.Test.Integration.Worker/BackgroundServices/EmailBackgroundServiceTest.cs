using System.Reflection;
using System.Text;
using System.Text.Json;
using NotificationService.Test.Integration.Worker.Collections;
using NotificationService.Worker.BackgroundServices;
using RabbitMQ.Client;
using Shared.Aws.Helpers.Test;
using Shared.Constants;
using Shared.RabbitMq.Notifications.Configurations;
using Shared.RabbitMq.Notifications.Messages;
using Shared.Test.Generators;
using Shared.Test.Helpers.Structures;

namespace NotificationService.Test.Integration.Worker.BackgroundServices
{
    [Collection(nameof(WorkerCollection))]
    public class EmailBackgroundServiceTest
    {
        private const int _timeoutInSeconds = 5;

        private readonly WorkerCollectionCluster _collectionCluster;

        public EmailBackgroundServiceTest(WorkerCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReceivedAsync_WhenMessageIsNotSerialized_ShouldPutMessageInDlq(bool isJson)
        {
            // Arrange
            var mainQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.QueueName);
            var dlqMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.DlqName);
            if (mainQueueMessageCount != 0 || dlqMessageCount != 0)
                Assert.Fail($"The queues are not empty. Main: {mainQueueMessageCount}, DLQ: {dlqMessageCount}.");

            string message = isJson ? "{}" : "Test message";
            await _collectionCluster.RabbitMqFixture.PublishMessageAsync(
                EmailConfiguration.ExchangeName,
                EmailConfiguration.RoutingKey,
                new BasicProperties()
                {
                    CorrelationId = Guid.NewGuid().ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                },
                JsonSerializer.SerializeToUtf8Bytes(message));

            // Act
            // EmailBackgroundService should automatically handle the message.

            // Assert
            var elapsedTime = 0;
            while (true)
            {
                dlqMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.DlqName);
                if (dlqMessageCount == 1)
                    break;

                if (elapsedTime >= _timeoutInSeconds)
                    Assert.Fail($"The test waited for {_timeoutInSeconds} seconds, but the message was not in the DLQ.");

                await Task.Delay(1000);
                ++elapsedTime;
            }

            var messageFromRabbitMq = await _collectionCluster.RabbitMqFixture.GetNextMessageAsync(EmailConfiguration.DlqName);
            Assert.NotNull(messageFromRabbitMq);
            Assert.Equal(message, JsonSerializer.Deserialize<string>(Encoding.UTF8.GetString(messageFromRabbitMq.Body.ToArray())));

            mainQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.QueueName);
            var retryQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.RetryQueueName);
            Assert.Equal((uint)0, mainQueueMessageCount);
            Assert.Equal((uint)0, retryQueueMessageCount);

            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.QueueName);
            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.RetryQueueName);
            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.DlqName);
        }

        [Fact]
        public async Task ReceivedAsync_WhenEmailIsSent_ShouldAcknowledgeMessage()
        {
            // Arrange
            var mainQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.QueueName);
            if (mainQueueMessageCount != 0)
                Assert.Fail($"The main queue is not empty. Message Count: {mainQueueMessageCount}.");

            var to = EmailGenerator.Generate();
            var bodyParamValue = "Test message";
            var message = new EmailMessage(
                [to],
                TemplateId: WorkerCollectionCluster.AwsSesTemplatePrefix,
                SupportedLanguages.DefaultLanguage,
                Parameters: new Dictionary<string, string>()
                {
                    { WorkerCollectionCluster.AwsSesTemplateBodyParameter, bodyParamValue }
                });

            await _collectionCluster.RabbitMqFixture.PublishMessageAsync(
                EmailConfiguration.ExchangeName,
                EmailConfiguration.RoutingKey,
                new BasicProperties()
                {
                    CorrelationId = Guid.NewGuid().ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                },
                JsonSerializer.SerializeToUtf8Bytes(message));

            // Act
            // EmailBackgroundService should automatically handle the message.

            // Assert
            var elapsedTime = 0;
            SesMessage? sentEmail = null;
            while (true)
            {
                var sentEmails = await _collectionCluster.AwsFixture.SES_GetSentEmailsAsync();
                sentEmail = sentEmails.FirstOrDefault(e => e.To[0] == to);
                if (sentEmail != null)
                    break;

                if (elapsedTime >= _timeoutInSeconds)
                    Assert.Fail($"The test waited for {_timeoutInSeconds} seconds, but the email was not sent.");

                await Task.Delay(1000);
                ++elapsedTime;
            }
            
            Assert.Equal(AwsTestConstants.SesFromEmail, sentEmail.Source);
            Assert.Equal(WorkerCollectionCluster.AwsSesTemplateSubjectLine, sentEmail.Subject);
            Assert.Equal(WorkerCollectionCluster.AwsSesTemplateBody.Replace($"{{{{{WorkerCollectionCluster.AwsSesTemplateBodyParameter}}}}}", bodyParamValue), sentEmail.BodyText);

            mainQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.QueueName);
            var retryQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.RetryQueueName);
            var dlqMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.DlqName);
            Assert.Equal((uint)0, mainQueueMessageCount);
            Assert.Equal((uint)0, retryQueueMessageCount);
            Assert.Equal((uint)0, dlqMessageCount);

            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.QueueName);
            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.RetryQueueName);
            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.DlqName);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        public async Task ReceivedAsync_WhenEmailIsNotSent_ShouldPutMessageInRetryQueueWithProperExpiration(int retryCount)
        {
            // Arrange
            var mainQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.QueueName);
            var retryQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.RetryQueueName);
            if (mainQueueMessageCount != 0 || retryQueueMessageCount != 0)
                Assert.Fail($"The queues are not empty. Main: {mainQueueMessageCount}, Retry: {retryQueueMessageCount}.");

            var message = new EmailMessage(
                To: [EmailGenerator.Generate()],
                TemplateId: StringGenerator.GenerateAlphanumeric(),
                SupportedLanguages.DefaultLanguage,
                Parameters: null);

            var properties = new BasicProperties()
            {
                CorrelationId = Guid.NewGuid().ToString(),
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            if (retryCount != 0)
            {
                properties.Headers = new Dictionary<string, object?>()
                {
                    { "Retry-Count", retryCount }
                };
            }

            await _collectionCluster.RabbitMqFixture.PublishMessageAsync(
                EmailConfiguration.ExchangeName,
                EmailConfiguration.RoutingKey,
                properties,
                body: JsonSerializer.SerializeToUtf8Bytes(message));

            // Act
            // EmailBackgroundService should automatically handle the message.

            // Assert
            var elapsedTime = 0;
            while (true)
            {
                retryQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.RetryQueueName);
                if (retryQueueMessageCount == 1)
                    break;

                if (elapsedTime >= _timeoutInSeconds)
                    Assert.Fail($"The test waited for {_timeoutInSeconds} seconds, but the message was not in the retry queue.");

                await Task.Delay(1000);
                ++elapsedTime;
            }

            var emailBackgroundService = _collectionCluster.NotificationWebApp.GetBackgroundService<EmailBackgroundService>();
            var retryDelayTimeFieldInfo = typeof(EmailBackgroundService)
                .GetField("_retryDelayTime", BindingFlags.NonPublic | BindingFlags.Instance)!;
            var retryDelayTime = (TimeSpan)retryDelayTimeFieldInfo.GetValue(emailBackgroundService)!;

            var messageFromRabbitMq = await _collectionCluster.RabbitMqFixture.GetNextMessageAsync(EmailConfiguration.RetryQueueName);
            Assert.NotNull(messageFromRabbitMq);
            Assert.Equal(retryCount + 1, (int)messageFromRabbitMq.BasicProperties.Headers!["Retry-Count"]!);
            Assert.Equal(((int)retryDelayTime.TotalMilliseconds * (retryCount + 1)).ToString(), messageFromRabbitMq.BasicProperties.Expiration);

            mainQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.QueueName);
            var dlqMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.DlqName);
            Assert.Equal((uint)0, mainQueueMessageCount);
            Assert.Equal((uint)0, dlqMessageCount);

            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.QueueName);
            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.RetryQueueName);
            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.DlqName);
        }

        [Fact]
        public async Task ReceivedAsync_WhenEmailIsNotSentAndMessageExceedsRetryLimit_ShouldPutMessageInDlq()
        {
            // Arrange
            var mainQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.QueueName);
            var dlqMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.DlqName);
            if (mainQueueMessageCount != 0 || dlqMessageCount != 0)
                Assert.Fail($"The queues are not empty. Main: {mainQueueMessageCount}, DLQ: {dlqMessageCount}.");

            var message = new EmailMessage(
                To: [EmailGenerator.Generate()],
                TemplateId: StringGenerator.GenerateAlphanumeric(),
                SupportedLanguages.DefaultLanguage,
                Parameters: null);

            var maxRetryFieldInfo = typeof(EmailBackgroundService).GetField("_maxRetry", BindingFlags.NonPublic | BindingFlags.Static)!;
            var maxRetry = (int)maxRetryFieldInfo.GetValue(null)!;

            await _collectionCluster.RabbitMqFixture.PublishMessageAsync(
                EmailConfiguration.ExchangeName,
                EmailConfiguration.RoutingKey,
                new BasicProperties()
                {
                    CorrelationId = Guid.NewGuid().ToString(),
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
                    Headers = new Dictionary<string, object?>()
                    {
                        { "Retry-Count", maxRetry }
                    }
                },
                JsonSerializer.SerializeToUtf8Bytes(message));

            // Act
            // EmailBackgroundService should automatically handle the message.

            // Assert
            var elapsedTime = 0;
            while (true)
            {
                dlqMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.DlqName);
                if (dlqMessageCount == 1)
                    break;

                if (elapsedTime >= _timeoutInSeconds)
                    Assert.Fail($"The test waited for {_timeoutInSeconds} seconds, but the message was not in the retry queue.");

                await Task.Delay(1000);
                ++elapsedTime;
            }

            mainQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.QueueName);
            var retryQueueMessageCount = await _collectionCluster.RabbitMqFixture.GetMessageCountAsync(EmailConfiguration.RetryQueueName);
            Assert.Equal((uint)0, mainQueueMessageCount);
            Assert.Equal((uint)0, retryQueueMessageCount);

            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.QueueName);
            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.RetryQueueName);
            await _collectionCluster.RabbitMqFixture.ClearMessagesAsync(EmailConfiguration.DlqName);
        }
    }
}
