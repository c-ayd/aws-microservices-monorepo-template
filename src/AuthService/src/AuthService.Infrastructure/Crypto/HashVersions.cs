using System.Security.Cryptography;
using AuthService.Application.Abstractions.Crypto;
using Common.Crypto.Structures;

namespace AuthService.Infrastructure.Crypto
{
    public class HashVersions : IHashVersions
    {
        public byte CurrentVersion { get; private set; } = 1;

        public HashOptions GetHashOptions(byte version)
        {
            return _versions[version];
        }

        private static Dictionary<byte, HashOptions> _versions = new Dictionary<byte, HashOptions>()
        {
            { 1, new HashOptions(SHA256.Create, 32) }
        };
    }
}
