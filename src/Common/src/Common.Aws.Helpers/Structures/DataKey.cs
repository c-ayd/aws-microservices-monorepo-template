namespace Common.Aws.Helpers.Structures
{
    /// <summary>
    /// Holds information about the data encryption key for envelope encryption.
    /// </summary>
    /// <param name="Plaintext">Plaintext version of the data encryption key</param>
    /// <param name="Encrypted">Encrypted version of the data encryption key</param>
    public record DataKey(
        byte[] Plaintext,
        byte[] Encrypted
    );
}
