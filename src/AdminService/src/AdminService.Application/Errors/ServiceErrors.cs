using Common.Http.Response.Structures;

namespace AdminService.Application.Errors
{
    public static class ServiceErrors
    {
        public static readonly ErrorItem AccountNotFound = new ErrorItem("admin_account_not_found", "The account could not be found.");
    }
}
