using Shared.AspNetCore.Helpers.Options;

namespace NotificationService.Worker.Options
{
    public class SmtpOptions : IOptions
    {
        public static string Key => "Smtp";

        public required string AwsSesFromEmailAddress { get; set; }
    }
}
