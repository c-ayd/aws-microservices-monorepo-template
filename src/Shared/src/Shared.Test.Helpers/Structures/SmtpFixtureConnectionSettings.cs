namespace Shared.Test.Helpers.Structures
{
    public record SmtpFixtureConnectionSettings(
        string Email,
        string DisplayName,
        string Server,
        int Port,
        bool EnableSsl
    );
}
