using Common.AspNetCore.Helpers.Options;

namespace Common.Aws.Helpers.Options
{
    public class JwtKeysOptionsBase : IOptions
    {
        public static string Key => "JwtKeys";

        public required string CurrentKeyId { get; set; }
        public required List<KeyInfo> Keys { get; set; }

        public class KeyInfo
        {
            public required string KeyId { get; set; }
            public required string PublicKey { get; set; }
            public required string PrivateKey { get; set; }
            public required long NotBeforeTime { get; set; }
            public required long ExpirationTime { get; set; }
        }
    }
}
