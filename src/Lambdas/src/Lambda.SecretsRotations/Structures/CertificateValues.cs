namespace Lambda.SecretsRotations.Structures
{
    /// <summary>
    /// Holds certificate and its private key values in text format.
    /// </summary>
    /// <param name="Certificate">Value of the certificate</param>
    /// <param name="PrivateKey">Value of the private key</param>
    public record CertificateValues(
        string Certificate,
        string PrivateKey
    );
}
