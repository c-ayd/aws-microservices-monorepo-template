namespace AuthService.Application.Features.AccountEndpoints.ResetPassword
{
    public record ResetPasswordRequest(
        string? Password,
        bool? CloseSessions
    );
}
