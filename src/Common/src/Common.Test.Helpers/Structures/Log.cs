using Microsoft.Extensions.Logging;

namespace Common.Test.Structures
{
    /// <summary>
    /// Holds information about a log.
    /// </summary>
    /// <param name="LogLevel">Level of the log</param>
    /// <param name="EventId">Even ID of the log</param>
    /// <param name="Message">Messages of the log</param>
    /// <param name="Exception">Thrown exception if it is given to the log</param>
    public record Log(
        LogLevel LogLevel,
        EventId EventId,
        string Message,
        Exception? Exception
    );
}
