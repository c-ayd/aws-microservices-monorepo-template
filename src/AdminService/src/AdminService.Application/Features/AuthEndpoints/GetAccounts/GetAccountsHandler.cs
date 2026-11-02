using System.Net;
using Common.Http.Response;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Grpc.Auth.Admin;

namespace AdminService.Application.Features.AuthEndpoints.GetAccounts
{
    public class GetAccountsHandler
    {
        public static async Task<IResult> HandleAsync(
            [FromQuery] int page,
            AuthGrpcAdmin.AuthGrpcAdminClient client,
            CancellationToken cancellationToken)
        {
            var response = await client.GetAccountsAsync(new GetAccountsRequest()
            {
                Page = page
            }, cancellationToken: cancellationToken);

            return JsonResponseBuilder.SuccessWithData(HttpStatusCode.OK, response);
        }
    }
}
