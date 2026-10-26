using AuthService.Application.Dtos.Authentication;

namespace AuthService.Application.Abstractions.Authentication
{
    /// <summary>
    /// Provides methods to access the RSA private and public keys.
    /// </summary>
    public interface IJwtKeyService
    {
        JwtKeyDto GetActivePublicKey();
        JwtKeyDto GetActivePrivateKey();
        IReadOnlyList<JwtKeyDto> GetAllPublicKeys();
        IReadOnlyList<JwtKeyDto> GetAllPrivateKeys();
    }
}
