using System.Security.Cryptography;
using AuthService.Application.Abstractions.Authentication;
using AuthService.Application.Dtos.Authentication;
using AuthService.Application.Options;
using AuthService.Infrastructure.Exceptions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace AuthService.Infrastructure.Authentication
{
    public class JwtKeyService : IJwtKeyService
    {
        private List<JwtKeyDto> _privateKeys = new List<JwtKeyDto>();
        private List<JwtKeyDto> _publicKeys = new List<JwtKeyDto>();

        public JwtKeyService(IOptions<JwtKeysOptions> jwtKeysOptions)
        {
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

        public JwtKeyDto GetActivePublicKey()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var publicKey in _publicKeys)
            {
                if (publicKey.NotBeforeTime <= now && publicKey.ExpirationTime >= now)
                {
                    return publicKey;
                }
            }

            // This should never happen
            throw new NoActiveJwtKeyException("public key");
        }

        public JwtKeyDto GetActivePrivateKey()
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var privateKey in _privateKeys)
            {
                if (privateKey.NotBeforeTime <= now && privateKey.ExpirationTime >= now)
                {
                    return privateKey;
                }
            }

            // This should never happen
            throw new NoActiveJwtKeyException("private key");
        }

        public IReadOnlyList<JwtKeyDto> GetAllPublicKeys()
        {
            return _publicKeys.AsReadOnly();
        }

        public IReadOnlyList<JwtKeyDto> GetAllPrivateKeys()
        {
            return _privateKeys.AsReadOnly();
        }
    }
}
