using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Shared.Grpc.Auth.Admin;

namespace AuthService.Infrastructure.Grpc.Admin
{
    public partial class AuthGrpcAdminService
    {    
        public override async Task<GetAccountsResponse> GetAccounts(GetAccountsRequest request, ServerCallContext context)
        {
            var accounts = await _authDbContext.Accounts
                .OrderByDescending(a => a.CreatedDate)
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
                    DeletedDate = a.DeletedDate
                })
                .Skip((request.Page - 1) * _pageSize)
                .Take(_pageSize)
                .ToListAsync(context.CancellationToken);

            var response = new GetAccountsResponse();
            if (accounts.Count == 0)
                return response;

            foreach (var account in accounts)
            {
                response.Accounts.Add(account.GetAccountDto());
            }

            return response;
        }
    }
}
