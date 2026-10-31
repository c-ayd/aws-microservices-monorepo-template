namespace Common.Test.Helpers.Structures
{
    /// <summary>
    /// Holds information about a sent email via AWS SES.
    /// </summary>
    /// <param name="MessageId">ID of the sent email</param>
    /// <param name="Source">From email address of the sent email</param>
    /// <param name="To">Destionations of the sent email</param>
    /// <param name="Subject">Subject line of the sent email</param>
    /// <param name="BodyText">Body of the sent email</param>
    public record SesMessage(
        string? MessageId,
        string? Source,
        List<string> To,
        string? Subject,
        string? BodyText
    );
}
