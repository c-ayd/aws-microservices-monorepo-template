namespace Common.Crypto.Exceptions
{
    public class InvalidNumberOfBytesException : Exception
    {
        public InvalidNumberOfBytesException(int dataLength)
            : base($"The data length must be a positive number. Given value: {dataLength}")
        {
        }
    }
}
