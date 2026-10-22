using Amazon.Runtime;

namespace Shared.Aws.Helpers.Test
{
    public static class AwsTestConstants
    {
        public static readonly BasicAWSCredentials Credentials = new BasicAWSCredentials("test", "test");
        public const string Region = "us-east-1";

        public const string SesFromEmail = "test@test.com";
    }
}
