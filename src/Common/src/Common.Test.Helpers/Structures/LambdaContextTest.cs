using Amazon.Lambda.Core;

namespace Common.Test.Helpers.Structures
{
    /// <summary>
    /// Is a fake class inheriting <see cref="Amazon.Lambda.Core.ILambdaContext"/> for
    /// lambda function test cases.
    /// </summary>
    public class LambdaContextTest : ILambdaContext
    {
        public string AwsRequestId => string.Empty;
        public IClientContext ClientContext => new ClientContextTest();
        public string FunctionName => string.Empty;
        public string FunctionVersion => string.Empty;
        public ICognitoIdentity Identity => new CognitoIdentityTest();
        public string InvokedFunctionArn => string.Empty;
        public ILambdaLogger Logger => new LambdaLoggerTest();
        public string LogGroupName => string.Empty;
        public string LogStreamName => string.Empty;
        public int MemoryLimitInMB => 100;
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(300);
    }

    internal class LambdaLoggerTest : ILambdaLogger
    {
        public void Log(string message) { }
        public void LogLine(string message) { }
    }

    internal class ClientContextTest : IClientContext
    {
        public IDictionary<string, string> Environment => new Dictionary<string, string>();
        public IClientApplication Client => new ClientApplicationTest();
        public IDictionary<string, string> Custom => new Dictionary<string, string>();
    }

    internal class ClientApplicationTest : IClientApplication
    {
        public string AppPackageName => string.Empty;
        public string AppTitle => string.Empty;
        public string AppVersionCode => string.Empty;
        public string AppVersionName => string.Empty;
        public string InstallationId => string.Empty;
    }

    internal class CognitoIdentityTest : ICognitoIdentity
    {
        public string IdentityId => string.Empty;
        public string IdentityPoolId => string.Empty;
    }
}
