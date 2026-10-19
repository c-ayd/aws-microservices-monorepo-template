using System.Text.Json;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Microsoft.Extensions.Options;
using Shared.Aws.Helpers.Abstractions;
using Shared.Aws.Helpers.Options;

namespace Shared.Aws.Helpers
{
    internal class AwsSes : IEmailSender
    {
        private readonly IAmazonSimpleEmailServiceV2 _awsSes;
        private readonly string _fromEmail;

        public AwsSes(
            IAmazonSimpleEmailServiceV2 awsSes,
            IOptions<AwsSesOptions> awsSesOptions)
        {
            _awsSes = awsSes;
            _fromEmail = awsSesOptions.Value.FromEmailAddress;
        }

        public async Task<string> SendEmailAsync(
            List<string> to,
            string templateName,
            Dictionary<string, string>? templateParams,
            CancellationToken cancellationToken = default)
        {
            var response = await _awsSes.SendEmailAsync(new SendEmailRequest()
            {
                FromEmailAddress = _fromEmail,
                Destination = new Destination()
                {
                    ToAddresses = to
                },
                Content = new EmailContent()
                {
                    Template = new Template()
                    {
                        TemplateName = templateName,
                        TemplateData = JsonSerializer.Serialize(templateParams ?? new Dictionary<string, string>())
                    }
                }
            }, cancellationToken);

            return response.MessageId;
        }
    }
}
