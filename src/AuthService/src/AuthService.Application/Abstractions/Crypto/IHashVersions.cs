using Common.Crypto.Structures;

namespace AuthService.Application.Abstractions.Crypto
{
    /// <summary>
    /// Provides methods to get available hash versions.
    /// </summary>
    public interface IHashVersions
    {
        byte CurrentVersion { get; }

        /// <summary>
        /// Gets the hash options based on a given version.
        /// </summary>
        /// <param name="version">Version of the hash options</param>
        /// <returns>Returns the hash options.</returns>
        HashOptions GetHashOptions(byte version);
    }
}
