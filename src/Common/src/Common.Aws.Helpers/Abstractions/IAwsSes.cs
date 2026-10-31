namespace Common.Aws.Helpers.Abstractions
{
    /// <summary>
    /// Provides methods to interact with AWS SES.
    /// </summary>
    public interface IAwsSes
    {
        Task<string> SendEmailAsync(string fromEmail, List<string> to, string templateName, Dictionary<string, string>? templateParams, CancellationToken cancellationToken = default);
    }
}
