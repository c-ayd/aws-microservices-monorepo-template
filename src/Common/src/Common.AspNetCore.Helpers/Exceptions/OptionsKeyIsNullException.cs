namespace Common.AspNetCore.Helpers.Exceptions
{
    public class OptionsKeyIsNullException : Exception
    {
        public OptionsKeyIsNullException(string typeName)
            : base($"The key value of the options class {typeName} is null.")
        {            
        }
    }
}
