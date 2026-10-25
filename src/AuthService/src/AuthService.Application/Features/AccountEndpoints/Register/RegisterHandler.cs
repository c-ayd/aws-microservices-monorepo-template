using System.Net;
using AuthService.Application.Abstractions.Crypto;
using AuthService.Application.Abstractions.DbContexts;
using AuthService.Application.Abstractions.MessageBrokers;
using AuthService.Application.Options;
using AuthService.Domain.Entities;
using AuthService.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Crypto;
using Shared.Http.Response;
using Shared.Http.Response.Structures;
using Shared.RabbitMq.Notifications.Messages;
using Shared.RabbitMq.Notifications.Templates;

namespace AuthService.Application.Features.AccountEndpoints.Register
{
    public class RegisterHandler
    {
        public static async Task<IResult> HandleAsync(
            RegisterRequest request,
            IAuthDbContext authDbContext,
            IPasswordHasher passwordHasher,
            IHashVersions hashVersions,
            IOptions<TokenLifespansOptions> tokenLifespansOptions,
            IEmailService emailService,
            HttpContext context)
        {
            // Check if the account with the same email exists
            var account = await authDbContext.Accounts
                .Where(a => a.Email == request.Email)
                .Select(a => a.Email)
                .FirstOrDefaultAsync();

            if (account != null)
                return JsonResponseBuilder.Error(
                    HttpStatusCode.Conflict,
                    [
                        new ErrorItem("auth_email_in_use", "The email address is already in use.")
                    ]
                );

            // Create a new account and an email verification token and save them in the DB
            var newAccount = new Account(
                request.Email!,
                passwordHasher.Hash(request.Password!),
                (string)context.Items["PreferredLanguage"]!);

            var emailVerificationTokenValue = TokenGenerator.GenerateBase64UrlSafe();
            var emailVerificationToken = new Token(
                newAccount.Id,
                ETokenPurpose.EmailVerification,
                ValueHasher.Hash(emailVerificationTokenValue, hashVersions.CurrentVersion, hashVersions.GetHashOptions),
                DateTimeOffset.UtcNow.AddHours(tokenLifespansOptions.Value.EmailVerificationLifespanInHours));

            newAccount.Tokens.Add(emailVerificationToken);
            await authDbContext.Accounts.AddAsync(newAccount);
            await authDbContext.SaveChangesAsync();

            // Send an email verification message to the message broker
            await emailService.SendAsync(new EmailMessage(
                To: [request.Email!],
                TemplateId: EmailTemplates.EmailVerification,
                Language: newAccount.PreferredLanguage,
                Parameters: new Dictionary<string, string>()
                {
                    { "Token", emailVerificationTokenValue }
                })
            );

            return JsonResponseBuilder.Success(HttpStatusCode.NoContent);
        }
    }
}
