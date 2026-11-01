using Common.Http.Response.Structures;

namespace Common.Http.Helpers.Response
{
    /**
     * Due to the circular reference issue, Common.Http.Helpers keeps its own version of CommonErrors.
     */
    internal static class CommonErrors
    {
        public static readonly ErrorItem UnknownError = new ErrorItem("internal_server_error", "Something went wrong.");

        public static readonly ErrorItem BindingError = new ErrorItem("route_or_query_params_wrong", "The given route or query parameters are wrong.");
        public static readonly ErrorItem RequestBodyMissingError = new ErrorItem("request_body_missing", "The request body is missing");

        public static readonly ErrorItem UnauthorizedError = new ErrorItem("request_unauthorized", "Authentication is required.");
        public static readonly ErrorItem ForbiddenError = new ErrorItem("request_forbidden", "Access is denied.");
    }
}
