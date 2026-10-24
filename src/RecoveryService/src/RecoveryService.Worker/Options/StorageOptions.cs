using Shared.AspNetCore.Helpers.Options;

namespace RecoveryService.Worker.Options
{
    public class StorageOptions : IOptions
    {
        public static string Key => "Storage";

        public required string AwsS3BucketName { get; set; }
        public required List<string> Folders { get; set; }
    }
}
