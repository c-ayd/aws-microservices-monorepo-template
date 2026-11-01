using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Grpc.Auth.Admin;

namespace AuthService.Infrastructure.Grpc.Admin
{
    public partial class AuthGrpcAdminService
    {
        public override async Task<GetAccountResponse> GetAccount(GetAccountRequest request, ServerCallContext context)
        {
            var accountId = Guid.Parse(request.Id);
            var account = await _authDbContext.Accounts
                .Where(a => a.Id == accountId)
                .Select(a => new AccountDetailsDto
                {
                    CreatedDate = a.CreatedDate,
                    Email = a.Email,
                    IsEmailVerified = a.IsEmailVerified,
                    IsBanned = a.IsBanned,
                    IsLocked = a.IsLocked,
                    FailedLoginAttempts = a.FailedLoginAttempts,
                    UnlockDate = a.UnlockDate,
                    UpdatedDate = a.UpdatedDate,
                    IsDeleted = a.IsDeleted,
                    DeletedDate = a.DeletedDate,
                    Roles = a.Roles.Select(r => new RoleDetailsDto()
                    {
                        CreatedDate = r.CreatedDate,
                        Name = r.Name
                    }).ToList(),
                    Sessions = a.Sessions.Select(s => new SessionDetailsDto()
                    {
                        CreatedDate = s.CreatedDate,
                        ExpirationDate = s.ExpirationDate,
                        IpAddress = s.IpAddress,
                        DeviceInfo = s.DeviceInfo,
                        UpdatedDate = s.UpdatedDate
                    }).ToList(),
                    Tokens = a.Tokens.Select(t => new TokenDetailsDto()
                    {
                        CreatedDate = t.CreatedDate,
                        Purpose = t.Purpose,
                        ExpirationDate = t.ExpirationDate
                    }).ToList()
                })
                .FirstOrDefaultAsync();

            if (account == null)
                throw new RpcException(new Status(StatusCode.NotFound, $"The account with the {request.Id} ID is not found."));

            var response = new GetAccountResponse();
            response.Account = account.GetAccountDto();
            foreach (var role in account.Roles)
            {
                response.Roles.Add(role.GetRoleDto());
            }
            foreach (var session in account.Sessions)
            {
                response.Sessions.Add(session.GetSessionDto());
            }
            foreach (var token in account.Tokens)
            {
                response.Tokens.Add(token.GetTokenDto());
            }

            return response;
        }
    }
}
