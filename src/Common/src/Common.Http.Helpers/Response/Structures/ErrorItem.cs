namespace Common.Http.Response.Structures
{
    /// <summary>
    /// Represents an error item in the error response.
    /// </summary>
    /// <param name="Code">Code of the error</param>
    /// <param name="Message">Human readable message of the error</param>
    /// <param name="Metadata">Additional information about the error</param>
    public record ErrorItem(
        string? Code = null,
        string? Message = null,
        object? Metadata = null
    );
}
