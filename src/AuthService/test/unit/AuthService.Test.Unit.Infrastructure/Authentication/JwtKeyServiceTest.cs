using AuthService.Infrastructure.Authentication;
using AuthService.Application.Options;
using Microsoft.Extensions.Options;

namespace AuthService.Test.Unit.Infrastructure.Authentication
{
    public class JwtKeyServiceTest
    {
        public static readonly JwtKeysOptions _jwtKeysOptions = new JwtKeysOptions()
        {
            CurrentKeyId = "k2",
            Keys = new List<JwtKeysOptions.KeyInfo>()
            {
                new JwtKeysOptions.KeyInfo()
                {
                    KeyId = "k1",
                    ExpirationTime = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                    NotBeforeTime = DateTimeOffset.UtcNow.AddMonths(-3).ToUnixTimeSeconds(),
                    PrivateKey = File.ReadAllText("./test-jwt-k1-private.pem"),
                    PublicKey = File.ReadAllText("./test-jwt-k1-public.pem")
                },
                new JwtKeysOptions.KeyInfo()
                {
                    KeyId = "k2",
                    ExpirationTime = DateTimeOffset.UtcNow.AddMonths(3).ToUnixTimeSeconds(),
                    NotBeforeTime = DateTimeOffset.UtcNow.AddDays(-1).ToUnixTimeSeconds(),
                    PrivateKey = File.ReadAllText("./test-jwt-k2-private.pem"),
                    PublicKey = File.ReadAllText("./test-jwt-k2-public.pem")
                }
            }
        };

        [Fact]
        public void Constructor_WhenServiceIsInstantiated_ShouldLoadKeys()
        {
            // Act
            var jwtKeyService = new JwtKeyService(Options.Create(_jwtKeysOptions));

            // Assert
            var privateKeys = jwtKeyService.GetAllPrivateKeys();
            var publicKeys = jwtKeyService.GetAllPublicKeys();

            Assert.Equal(2, privateKeys.Count);
            Assert.Equal(2, publicKeys.Count);

            foreach (var privateKey in privateKeys)
            {
                Assert.NotNull(privateKey.Id);
                Assert.NotNull(privateKey.Key);
                Assert.Null(privateKey.Parameters);
            }

            foreach (var publicKey in publicKeys)
            {
                Assert.NotNull(publicKey.Id);
                Assert.NotNull(publicKey.Key);
                Assert.NotNull(publicKey.Parameters);
                Assert.NotNull(publicKey.Parameters.Value.Modulus);
                Assert.NotNull(publicKey.Parameters.Value.Exponent);
            }
        }
    }
}
