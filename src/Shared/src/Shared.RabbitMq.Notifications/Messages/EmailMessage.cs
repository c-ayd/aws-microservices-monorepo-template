namespace Shared.RabbitMq.Notifications.Messages
{
    public record EmailMessage(
        List<string> To,
        string TemplateId,
        string? Language,
        Dictionary<string, string>? Parameters
    );
}
