namespace Shared.Aws.Helpers.Structures
{
    public record DataKey(
        byte[] Plaintext,
        byte[] Encrypted
    );
}
