namespace Shared.Aws.Helpers.Structures
{
    public record EncryptionKey(
        byte[] Dek,
        byte[] DekEncrypted
    );
}
