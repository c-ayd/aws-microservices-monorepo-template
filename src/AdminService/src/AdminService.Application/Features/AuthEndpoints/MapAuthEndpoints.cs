using AdminService.Application.Features.AuthEndpoints.GetAccount;
using Common.Http.Helpers.Authentication;
using Microsoft.AspNetCore.Builder;

namespace AdminService.Application.Features.AuthEndpoints
{
    public static class MapEndpoints
    {
        public static void MapAuthEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/auth")
                .RequireAuthorization(Policies.AdminPolicy.PolicyName);

            group.MapGet("/account/{accountId}", GetAccountHandler.HandleAsync);
        }
    }
}
