using System.Net;
using AuthService.Application.Abstractions.DbContexts;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Shared.Http.Response;

namespace AuthService.Application.Features.AccountEndpoints.GetSessions
{
    public class GetSessionsHandler
    {
        public static async Task<IResult> HandleAsync(
            HttpContext context,
            IAuthDbContext authDbContext,
            CancellationToken cancellationToken)
        {
            var sessions = await authDbContext.Sessions
                .Where(s => s.AccountId == Guid.Parse(context.User.Identity!.Name!) &&
                    s.ExpirationDate > DateTimeOffset.UtcNow)
                .OrderByDescending(s => s.CreatedDate)
                .Select(s => new
                {
                    s.CreatedDate,
                    s.UpdatedDate,
                    s.IpAddress,
                    s.DeviceInfo
                })
                .ToListAsync(cancellationToken);

            return JsonResponseBuilder.SuccessWithData(HttpStatusCode.OK, sessions);
        }
    }
}
