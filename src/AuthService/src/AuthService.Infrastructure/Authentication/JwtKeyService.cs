using System.Security.Cryptography;
using AuthService.Application.Abstractions.Authentication;
using AuthService.Application.Dtos.Authentication;
using AuthService.Application.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Infrastructure.Authentication
{
    public class JwtKeyService : IJwtKeyService
    {
        public string CurrentKeyId { get; private set; }

        private List<JwtKeyDto> _privateKeys = new List<JwtKeyDto>();
        private List<JwtKeyDto> _publicKeys = new List<JwtKeyDto>();

        public JwtKeyService(IOptions<JwtKeysOptions> jwtKeysOptions)
        {
            CurrentKeyId = jwtKeysOptions.Value.CurrentKeyId;

            foreach (var keyInfo in jwtKeysOptions.Value.Keys)
            {
                var rsa = RSA.Create();
                rsa.ImportFromPem(keyInfo.PrivateKey);
                _privateKeys.Add(new JwtKeyDto()
                {
                    Id = keyInfo.KeyId,
                    ExpirationTime = keyInfo.ExpirationTime,
                    NotBeforeTime = keyInfo.NotBeforeTime,
                    Key = new RsaSecurityKey(rsa) { KeyId = keyInfo.KeyId },
                    Parameters = null
                });

                rsa = RSA.Create();
                rsa.ImportFromPem(keyInfo.PublicKey);
                _publicKeys.Add(new JwtKeyDto()
                {
                    Id = keyInfo.KeyId,
                    ExpirationTime = keyInfo.ExpirationTime,
                    NotBeforeTime = keyInfo.NotBeforeTime,
                    Key = new RsaSecurityKey(rsa) { KeyId = keyInfo.KeyId },
                    Parameters = rsa.ExportParameters(false)
                });
            }
        }

        public JwtKeyDto? GetPublicKey(string keyId)
        {
            return _publicKeys.FirstOrDefault(k => k.Id == keyId);
        }

        public JwtKeyDto? GetPrivateKey(string keyId)
        {
            return _privateKeys.FirstOrDefault(k => k.Id == keyId);
        }

        public IReadOnlyCollection<JwtKeyDto> GetAllPublicKeys()
        {
            return _publicKeys.AsReadOnly();
        }

        public IReadOnlyCollection<JwtKeyDto> GetAllPrivateKeys()
        {
            return _privateKeys.AsReadOnly();
        }
    }
}
