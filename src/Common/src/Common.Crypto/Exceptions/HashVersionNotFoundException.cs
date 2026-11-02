namespace Common.Crypto.Exceptions
{
    public class HashVersionNotFoundException : Exception
    {
        public HashVersionNotFoundException(byte version)
            : base($"Version {version} could not be found.")
        {
        }
    }
}
