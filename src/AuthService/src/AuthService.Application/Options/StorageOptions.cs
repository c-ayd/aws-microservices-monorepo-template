using Shared.AspNetCore.Helpers.Options;

namespace AuthService.Application.Options
{
    public class StorageOptions : IOptions
    {
        public static string Key => "Storage";

        public required string AwsS3BucketName { get; set; }
    }
}
