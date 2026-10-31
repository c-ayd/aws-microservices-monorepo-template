using System.Text.Json;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using Common.Aws.Helpers.Abstractions;

namespace Common.Aws.Helpers
{
    internal class AwsSes : IAwsSes
    {
        private readonly IAmazonSimpleEmailServiceV2 _awsSes;

        public AwsSes(IAmazonSimpleEmailServiceV2 awsSes)
        {
            _awsSes = awsSes;
        }

        public async Task<string> SendEmailAsync(
            string fromEmail,
            List<string> to,
            string templateName,
            Dictionary<string, string>? templateParams,
            CancellationToken cancellationToken = default)
        {
            var response = await _awsSes.SendEmailAsync(new SendEmailRequest()
            {
                FromEmailAddress = fromEmail,
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
