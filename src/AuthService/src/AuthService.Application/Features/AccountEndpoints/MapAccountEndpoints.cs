using AuthService.Application.Features.AccountEndpoints.CloseSession;
using AuthService.Application.Features.AccountEndpoints.CloseSessions;
using AuthService.Application.Features.AccountEndpoints.GetSessions;
using AuthService.Application.Features.AccountEndpoints.Login;
using AuthService.Application.Features.AccountEndpoints.Logout;
using AuthService.Application.Features.AccountEndpoints.Register;
using Microsoft.AspNetCore.Builder;
using Shared.Http.DependencyInjection;

namespace AuthService.Application.Features.AccountEndpoints
{
    public static class MapEndpoints
    {
        public static void MapAccountEndpoints(this WebApplication app)
        {
            var group = app.MapGroup("/accounts");

            group.MapPost("/register", RegisterHandler.HandleAsync)
                .AddValidation<RegisterRequest>();
            
            group.MapPost("/login", LoginHandler.HandleAsync)
                .AddValidation<LoginRequest>();

            group.MapDelete("/logout", LogoutHandler.HandleAsync)
                .RequireAuthorization();    // Since the endpoint deletes open session, it requires authorization.

            group.MapGet("/sessions", GetSessionsHandler.HandleAsync)
                .RequireAuthorization();
            group.MapPost("/session/{sessionId}/close", CloseSessionHandler.HandleAsync)
                .RequireAuthorization()
                .AddValidation<CloseSessionRequest>();
            group.MapPost("/sessions/close", CloseSessionsHandler.HandleAsync)
                .RequireAuthorization()
                .AddValidation<CloseSessionsRequest>();
        }
    }
}
