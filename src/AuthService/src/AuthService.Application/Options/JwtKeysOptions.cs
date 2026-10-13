using Shared.AspNetCore.Helpers.Options;

namespace AuthService.Application.Options
{
    public class JwtKeysOptions : IOptions
    {
        public static string Key => "JwtKeys";

        public required string CurrentKeyId { get; set; }
        public required List<KeyInfo> Keys { get; set; }

        public class KeyInfo
        {
            public required string KeyId { get; set; }
            public required string PrivateKey { get; set; }
            public required string PublicKey { get; set; }
        }
    }
}
