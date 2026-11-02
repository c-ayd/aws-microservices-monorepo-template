using Grpc.Core;
using Shared.Grpc.Auth.Admin;

namespace AdminService.Test.Integration.Application.Collections.GrpcServices
{
    public class AuthGrpcAdminTestService : AuthGrpcAdmin.AuthGrpcAdminBase
    {
        public override Task<GetAccountResponse> GetAccount(GetAccountRequest request, ServerCallContext context)
        {
            if (Guid.Parse(request.Id) == Guid.Empty)
                throw new RpcException(new Status(StatusCode.NotFound, "Account not found"));

            return Task.FromResult(new GetAccountResponse());
        }

        public override Task<GetAccountsResponse> GetAccounts(GetAccountsRequest request, ServerCallContext context)
        {
            var response = new GetAccountsResponse();
            response.Accounts.Add(new AccountDto());
            response.Accounts.Add(new AccountDto());
            response.Accounts.Add(new AccountDto());
            
            return Task.FromResult(response);
        }
    }
}
