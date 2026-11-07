namespace Lambda.SecretsRotations.Structures
{
    /// <summary>
    /// Holds information related to intermediate CA.
    /// </summary>
    /// <param name="Certificate">Value of the certificate</param>
    /// <param name="PrivateKey">Value of the private key</param>
    /// <param name="Password">Password for the private key</param>
    public record IntermediateCA(
        string Certificate,
        string PrivateKey,
        string Password
    );
}
