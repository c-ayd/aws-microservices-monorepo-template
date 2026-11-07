namespace Common.Test.Helpers.Exceptions
{
    public class PortArgumentException : Exception
    {
        public PortArgumentException(string? reason = null)
            : base($"A port number must be given. {reason}")
        {
        }
    }
}
