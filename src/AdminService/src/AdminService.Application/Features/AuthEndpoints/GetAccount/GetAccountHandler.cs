using System.Net;
using AdminService.Application.Errors;
using Common.Http.Response;
using Grpc.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Grpc.Auth.Admin;

namespace AdminService.Application.Features.AuthEndpoints.GetAccount
{
    public class GetAccountHandler
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
                            errors: [
                                ServiceErrors.AccountNotFound
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
