namespace AuthService.Infrastructure.Exceptions
{
    public class NoActiveJwtKeyException : Exception
    {
        public NoActiveJwtKeyException(string keyType)
            : base($"No active {keyType} JWT key could be found.")
        {
        }
    }
}
