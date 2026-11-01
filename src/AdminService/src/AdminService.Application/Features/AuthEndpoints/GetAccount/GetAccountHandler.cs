using System.Net;
using Common.Http.Response;
using Common.Http.Response.Structures;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Grpc.Auth.Admin;

namespace AdminService.Application.Features.AuthEndpoints.GetAccount
{
    public static class GetAccountHandler
    {
        public static async Task<IResult> HandleAsync(
            [FromRoute] Guid accountId,
            AuthGrpcAdmin.AuthGrpcAdminClient client)
        {
            GetAccountResponse response;
            try
            {
                response = await client.GetAccountAsync(new GetAccountRequest()
                {
                    Id = accountId.ToString()
                });
            }
            catch (RpcException exception)
            {
                switch (exception.StatusCode)
                {
                    case StatusCode.NotFound:
                        return JsonResponseBuilder.Error(
                            HttpStatusCode.NotFound,
                            [
                                new ErrorItem("admin_account_not_found", "The account could not be found.")
                            ]
                        );
                    default:
                        throw;
                }
            }

            return JsonResponseBuilder.SuccessWithData(HttpStatusCode.OK, response);
        }
    }
}
