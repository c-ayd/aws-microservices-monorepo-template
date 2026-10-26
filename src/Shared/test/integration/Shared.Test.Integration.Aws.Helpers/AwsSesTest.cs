using Shared.Aws.Helpers.Abstractions;
using Shared.Aws.Helpers.Test;
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
            var subjectLine = "Subject Line, {{param1}}";
            var subjectLineParameterValue = "test1";
            var body = "Body - {{param2}}";
            var bodyParameterValue = "test2";
            await _collectionCluster.AwsFixture.SES_CreateTemplateAsync(templateName, subjectLine, body);

            var awsSes = _collectionCluster.WebApiFixture.GetService<IAwsSes>();

            var to = EmailGenerator.Generate();
            var templateParams = new Dictionary<string, string>()
            {
                { "param1", subjectLineParameterValue },
                { "param2", bodyParameterValue }
            };

            // Act
            var messageId = await awsSes.SendEmailAsync(
                AwsTestConstants.SesFromEmail,
                [to],
                templateName,
                templateParams);

            // Assert
            var sentEmails = await _collectionCluster.AwsFixture.SES_GetSentEmailsAsync();

            Assert.NotNull(messageId);
            Assert.Single(sentEmails);
            Assert.Equal(messageId, sentEmails[0].MessageId);
            Assert.Equal(AwsTestConstants.SesFromEmail, sentEmails[0].Source);
            Assert.Equal(to, sentEmails[0].To[0]);
            Assert.Equal(subjectLine.Replace("{{param1}}", subjectLineParameterValue), sentEmails[0].Subject);
            Assert.Equal(body.Replace("{{param2}}", bodyParameterValue), sentEmails[0].BodyText);
        }
    }
}
