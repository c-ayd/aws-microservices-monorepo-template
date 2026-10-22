using Shared.Aws.Helpers.Abstractions;
using Shared.Test.Generators;
using Shared.Test.Integration.Aws.Helpers.Collections;

namespace Shared.Test.Integration.Aws.Helpers
{
    [Collection(nameof(AwsCollection))]
    public class AwsSesTest
    {
        private readonly AwsCollectionCluster _collectionCluster;

        public AwsSesTest(AwsCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task SendEmailAsync_WhenMethodIsCalled_ShouldSendEmail()
        {
            // Arrange
            var templateName = "aws-ses/test-template";
            var subject = "Subject Line, {{param1}}";
            var body = "Body {{param2}} - {{param3}}";
            await _collectionCluster.AwsFixture.SES_CreateTemplateAsync(templateName, subject, body);

            var awsSes = _collectionCluster.WebApiFixture.GetService<IEmailSender>();

            var to = EmailGenerator.Generate();
            var templateParams = new Dictionary<string, string>()
            {
                { "param1", "test1" },
                { "param2", "test2" },
                { "param3", "test3" }
            };

            // Act
            var messageId = await awsSes.SendEmailAsync([to], templateName, templateParams);

            // Assert
            var sentEmails = await _collectionCluster.AwsFixture.SES_GetSentEmailsAsync();

            Assert.NotNull(messageId);
            Assert.Single(sentEmails);
            Assert.Equal(messageId, sentEmails[0].MessageId);
            Assert.Equal("test@test.com", sentEmails[0].Source);
            Assert.Equal(to, sentEmails[0].To[0]);
            Assert.Equal("Subject Line, test1", sentEmails[0].Subject);
            Assert.Equal("Body test2 - test3", sentEmails[0].BodyText);
        }
    }
}
