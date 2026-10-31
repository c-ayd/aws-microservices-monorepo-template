using System.Security.Cryptography;

namespace Common.Crypto.Structures
{
    /// <summary>
    /// Represents hashing options that are used in <see cref="ValueHasher"/>.
    /// </summary>
    /// <param name="Algorithm">Type of the hashing algorithm</param>
    /// <param name="SaltSize">Size of the salt</param>
    public record HashOptions(
        Func<HashAlgorithm> Algorithm,
        int SaltSize
    );
}
