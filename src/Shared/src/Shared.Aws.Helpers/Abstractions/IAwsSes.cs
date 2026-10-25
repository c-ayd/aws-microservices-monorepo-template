namespace Shared.Aws.Helpers.Abstractions
{
    public interface IAwsSes
    {
        Task<string> SendEmailAsync(string fromEmail, List<string> to, string templateName, Dictionary<string, string>? templateParams, CancellationToken cancellationToken = default);
    }
}
