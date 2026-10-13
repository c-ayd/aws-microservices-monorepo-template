using Shared.AspNetCore.Helpers.Options;

namespace ApiGateway.Web.Options
{
    public class JwtOptions : IOptions
    {
        public static string Key => "Jwt";

        public required string Authority { get; set; }
        public required string Audience { get; set; }
    }
}
