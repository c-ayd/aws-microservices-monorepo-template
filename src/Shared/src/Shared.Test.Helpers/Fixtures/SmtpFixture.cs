using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Shared.Test.Helpers.Structures;

namespace Shared.Test.Helpers.Fixtures
{
    public class SmtpFixture : ContainerFixtureBase
    {
        protected override IContainer Container => _container;
        private IContainer _container = null!;

        public async Task InitializeAsync()
        {
            _container = new ContainerBuilder("mailhog/mailhog:v1.0.1")
                .WithPortBinding(1025, true)
                .WithPortBinding(8025, true)
                .Build();
            await _container.StartAsync();
        }

        public SmtpFixtureConnectionSettings GetSmtpConnectionSettings()
        {
            return new SmtpFixtureConnectionSettings(
                Email: "test@test.com",
                DisplayName: "Test Display Name",
                Server: _container.Hostname,
                Port: _container.GetMappedPublicPort(1025),
                EnableSsl: false
            );
        }

        public async Task<List<SentEmail>> GetEmailsAsync()
        {
            using var http = new HttpClient()
            {
                BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(8025)}")
            };

            var responseRaw = await http.GetStringAsync("/api/v2/messages");
            var response = JsonSerializer.Deserialize<MailHogResponse>(responseRaw, new JsonSerializerOptions()
            {
                PropertyNameCaseInsensitive = true
            })!;

            await http.DeleteAsync("/api/v1/messages");

            var emails = new List<SentEmail>();
            foreach (var item in response.Items)
            {
                emails.Add(new SentEmail()
                {
                    From = item.Content?.Headers["From"]?[0],
                    To = item.Content?.Headers["To"][0]
                        .Split(',', StringSplitOptions.TrimEntries).ToList() ?? new List<string>(),
                    Subject = item.Content?.Headers["Subject"]?[0],
                    Body = item.Content?.Body
                });
            }

            return emails;
        }

        public async Task ClearEmailsAsync()
        {
            using var http = new HttpClient()
            {
                BaseAddress = new Uri($"http://{_container.Hostname}:{_container.GetMappedPublicPort(8025)}")
            };

            await http.DeleteAsync("/api/v1/messages");
        }

        public async Task DisposeAsync()
        {
            await _container.StopAsync();
            await _container.DisposeAsync();
        }

        public class SentEmail
        {
            public string? From { get; set; }
            public List<string> To { get; set; } = new List<string>();
            public string? Subject { get; set; }
            public string? Body { get; set; }
        }

        private class MailHogResponse
        {
            public List<MailHogEmail> Items { get; set; } = new List<MailHogEmail>();

            public class MailHogEmail
            {
                public MailHogContent? Content { get; set; }

                public class MailHogContent
                {
                    public Dictionary<string, List<string>> Headers { get; set; } = new Dictionary<string, List<string>>();
                    public string? Body { get; set; }
                }
            }
        }
    }
}
