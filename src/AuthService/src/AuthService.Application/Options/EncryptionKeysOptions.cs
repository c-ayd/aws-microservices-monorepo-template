using Shared.AspNetCore.Helpers.Options;

namespace AuthService.Application.Options
{
    public class EncryptionKeysOptions : IOptions
    {
        public static string Key => "EncryptionKeys";

        public required List<VersionKeyPair> AesGcm { get; set; }

        public class VersionKeyPair
        {
            public required ushort Version { get; set; }
            public required string Key { get; set; }
        }
    }
}
