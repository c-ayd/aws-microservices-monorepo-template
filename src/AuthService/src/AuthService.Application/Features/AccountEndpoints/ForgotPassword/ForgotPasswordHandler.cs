using System.Net;
using System.Text;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Abstractions.MessageBrokers;
using AuthService.Application.Errors;
using AuthService.Application.Options;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using Common.Crypto;
using Common.Http.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.RabbitMq.Notifications.Messages;
using Shared.RabbitMq.Notifications.Templates;

namespace AuthService.Application.Features.AccountEndpoints.ForgotPassword
{
    public class ForgotPasswordHandler
    {
        public static async Task<IResult> HandleAsync(
            ForgotPasswordRequest request,
            IAuthDbContext authDbContext,
            IHashVersions hashVersions,
            IOptions<TokenLifespansOptions> tokenLifespansOptions,
            IEmailService emailService,
            HttpContext context)
        {
            // Check if account exists
            var accountId = await authDbContext.Accounts
                .Where(a => a.Email == request.Email)
                .Select(a => a.Id)
                .FirstOrDefaultAsync();

            if (accountId == Guid.Empty)
                return JsonResponseBuilder.Error(
                    HttpStatusCode.NotFound,
                    errors: [
                        ServiceErrors.AccountNotFound
                    ]
                );

            // Create a new reset password token and save it to the DB
            var resetPasswordTokenValue = TokenGenerator.GenerateBase64UrlSafe();
            var resetPasswordToken = new Token(
                accountId,
                ETokenPurpose.ResetPassword,
                valueHashed: Convert.ToBase64String(ValueHasher.Hash(
                    value: Encoding.UTF8.GetBytes(resetPasswordTokenValue).AsSpan(),
                    hashVersions.CurrentVersion,
                    hashVersions.GetHashOptions,
                    out var salt)),
                DateTimeOffset.UtcNow.AddHours(tokenLifespansOptions.Value.ResetPasswordLifespanInMinutes));

            await authDbContext.Tokens.AddAsync(resetPasswordToken);
            await authDbContext.SaveChangesAsync();

            // Send an reset password message to the message broker.
            // Both plaintext token value and the salt value that was used to
            // hash this token value are combined in emails with the delimeter dot,
            // so that the reset password endpoint can find and compare token values.
            await emailService.SendAsync(new EmailMessage(
                To: [request.Email!],
                TemplateId: EmailTemplates.ForgotPassword,
                Language: (string)context.Items["PreferredLanguage"]!,
                Parameters: new Dictionary<string, string>()
                {
                    { "Token", $"{resetPasswordTokenValue}.{WebEncoders.Base64UrlEncode(salt)}" }
                }));

            return JsonResponseBuilder.Success(HttpStatusCode.OK);
        }
    }
}
