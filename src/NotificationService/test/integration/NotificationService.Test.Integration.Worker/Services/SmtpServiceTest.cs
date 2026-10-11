using NotificationService.Test.Integration.Worker.Collections;
using NotificationService.Worker.Abstractions;
using NotificationService.Worker.Services;
using Shared.Test.Generators;

namespace NotificationService.Test.Integration.Worker.Services
{
    [Collection(nameof(WorkerCollection))]
    public class SmtpServiceTest
    {
        private readonly WorkerCollectionCluster _collectionCluster;

        public SmtpServiceTest(WorkerCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        [Fact]
        public async Task SendAsync_WhenSendSingleEmail_ShouldSendSingleEmail()
        {
            // Arrange
            var smtpService = _collectionCluster.NotificationWebApp.GetService<IEmailService>();
            if (smtpService is not SmtpService)
                return;     // Smtp service is not used for sending email anymore. Another test case should take in place.

            var to = EmailGenerator.Generate();
            var subject = StringGenerator.GenerateAlphanumeric();
            var body = StringGenerator.GenerateAlphanumeric();

            // Act
            await smtpService.SendAsync([to], subject, body, isBodyHtml: false);
            
            // Assert
            var emails = await _collectionCluster.SmtpFixture.GetEmailsAsync();
            Assert.Single(emails);

            var smtpConnSettings = _collectionCluster.SmtpFixture.GetSmtpConnectionSettings();
            Assert.Equal($"\"{smtpConnSettings.DisplayName}\" <{smtpConnSettings.Email}>",
                emails[0].From);

            Assert.Single(emails[0].To);
            Assert.Equal(to, emails[0].To[0]);

            Assert.Equal(subject, emails[0].Subject);
            Assert.Equal(body, emails[0].Body!.TrimEnd());
        }

        [Fact]
        public async Task SendAsync_WhenSendMultipleEmails_ShouldSendMultipleEmails()
        {
            // Arrange
            var smtpService = _collectionCluster.NotificationWebApp.GetService<IEmailService>();
            if (smtpService is not SmtpService)
                return;     // Smtp service is not used for sending email anymore. Another test case should take in place.
            
            var to = new string[]
            {
                EmailGenerator.Generate(),
                EmailGenerator.Generate(),
                EmailGenerator.Generate()
            };
            var subject = StringGenerator.GenerateAlphanumeric();
            var body = StringGenerator.GenerateAlphanumeric();

            // Act
            await smtpService.SendAsync(to, subject, body, isBodyHtml: false);

            // Assert
            var emails = await _collectionCluster.SmtpFixture.GetEmailsAsync();
            Assert.Single(emails);

            var smtpConnSettings = _collectionCluster.SmtpFixture.GetSmtpConnectionSettings();
            Assert.Equal($"\"{smtpConnSettings.DisplayName}\" <{smtpConnSettings.Email}>",
                emails[0].From);

            foreach (var item in emails[0].To)
            {
                if (!to.Contains(item))
                    Assert.Fail("The email is not sent to one of the given email addresses");
            }

            Assert.Equal(subject, emails[0].Subject);
            Assert.Equal(body, emails[0].Body!.TrimEnd());
        }
    }
}
