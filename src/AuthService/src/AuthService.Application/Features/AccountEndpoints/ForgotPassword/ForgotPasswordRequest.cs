namespace AuthService.Application.Features.AccountEndpoints.ForgotPassword
{
    public record ForgotPasswordRequest(
        string? Email
    );
}
