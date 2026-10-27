namespace Shared.RabbitMq.Notifications.Messages
{
    /// <summary>
    /// Holds necessary information about sending emails.
    /// </summary>
    /// <param name="To">Destionation email addresses</param>
    /// <param name="TemplateId">ID of the template without the language postfix</param>
    /// <param name="Language">Language of the template</param>
    /// <param name="Parameters">Parameter values of the template</param>
    public record EmailMessage(
        List<string> To,
        string TemplateId,
        string? Language,
        Dictionary<string, string>? Parameters
    );
}
