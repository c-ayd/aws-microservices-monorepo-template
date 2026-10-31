namespace Common.Crypto.Exceptions
{
    public class HashLengthMismatchException : Exception
    {
        public HashLengthMismatchException(int expectedLength, int hashLength)
            : base($"The hashed value expected to be {expectedLength} bytes long, however, it has {hashLength} bytes.")
        {
        }
    }
}
