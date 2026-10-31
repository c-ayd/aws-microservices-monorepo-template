using Common.AspNetCore.Helpers.Options;

namespace AuthService.Application.Options
{
    public class EncrpytionOptions : IOptions
    {
        public static string Key => "Encryption";

        public required string DroppedMessagesKeyAlias { get; set; }
    }
}
