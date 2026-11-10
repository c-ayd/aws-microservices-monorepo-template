namespace Lambda.SecretsRotations.Exceptions
{
    public class SecretNotFoundException : Exception
    {
        public SecretNotFoundException(string secretId)
            : base($"The secret with the {secretId} ID could not be found.")
        {
        }
    }
}
