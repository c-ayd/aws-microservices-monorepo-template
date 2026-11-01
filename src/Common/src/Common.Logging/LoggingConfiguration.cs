namespace Common.Logging
{
    public static class LoggingConfiguration
    {
        public static List<string> IgnoredEndpoints = new List<string>()
        {
            "/.well-known"
        };
    }
}
