using Amazon.Lambda.Core;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Lambda.SecretsRotations;

public class Function
{
    public async Task<Dictionary<string, string>> FunctionHandler(string input, ILambdaContext context)
    {
        
    }
}
