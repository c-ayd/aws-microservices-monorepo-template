using Microsoft.Extensions.Logging;
using Common.Test.Structures;

namespace Common.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is a null logger for test cases requiring a logger.
    /// </summary>
    /// <typeparam name="T">Type of the class requiring a logger</typeparam>
    public class LoggerFixture<T> : ILogger<T>
        where T : class
    {
        public List<Log> Logs = new List<Log>();

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Logs.Add(new Log(
                logLevel,
                eventId,
                formatter(state, exception),
                exception
            ));
        }
    }
}
