using Shared.AspNetCore.Helpers.Options;

namespace NotificationService.Worker.Options
{
    public class SmtpOptions : IOptions
    {
        public static string Key => "Smtp";

        public required string Username { get; set; }
        public required string Password { get; set; }
        public required string Email { get; set; }
        public required string DisplayName { get; set; }
        public required string Server { get; set; }
        public required int Port { get; set; }
        public required bool EnableSsl { get; set; }
    }
}
