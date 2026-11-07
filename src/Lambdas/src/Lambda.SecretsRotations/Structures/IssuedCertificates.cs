namespace Lambda.SecretsRotations.Structures
{
    /// <summary>
    /// Holds certificates of a service.
    /// </summary>
    /// <param name="Server">Server certificate</param>
    /// <param name="Client">Client certificate</param>
    public record IssuedCertificates(
        CertificateValues Server,
        CertificateValues? Client
    );
}
