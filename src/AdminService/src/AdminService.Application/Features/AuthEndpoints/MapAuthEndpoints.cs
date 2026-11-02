using AdminService.Application.Features.AuthEndpoints.GetAccount;
using AdminService.Application.Features.AuthEndpoints.GetAccounts;
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

            group.MapGet("/accounts", GetAccountsHandler.HandleAsync);
            group.MapGet("/account/{accountId}", GetAccountHandler.HandleAsync);
        }
    }
}
