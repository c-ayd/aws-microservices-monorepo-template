using System.Net.Http.Json;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Shared.Test.Helpers.Fixtures
{
    public class AwsFixture
    {
        private IContainer _container = null!;

        private string _endpoint = null!;
        private AmazonSecretsManagerClient _awsSecretsManagerClient = null!;
        private AmazonKeyManagementServiceClient _awsKmsClient = null!;
        private AmazonSimpleEmailServiceV2Client _awsSesClient = null!;
        private AmazonS3Client _awsS3BucketClient = null!;

        public async Task InitializeAsync()
        {
            _container = new ContainerBuilder("ministackorg/ministack:latest")
                .WithPortBinding(4566, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Running on"))
                .Build();

            await _container.StartAsync();

            // Configurations
            _endpoint = $"http://localhost:{GetPort()}";
            var credentials = new BasicAWSCredentials("test", "test");

            _awsSecretsManagerClient = new AmazonSecretsManagerClient(credentials,
                new AmazonSecretsManagerConfig
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = "us-east-1"
                });

            _awsKmsClient = new AmazonKeyManagementServiceClient(credentials,
                new AmazonKeyManagementServiceConfig
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = "us-east-1"
                });

            _awsSesClient = new AmazonSimpleEmailServiceV2Client(credentials,
                new AmazonSimpleEmailServiceV2Config
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = "us-east-1"
                });

            _awsS3BucketClient = new AmazonS3Client(credentials,
                new AmazonS3Config
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = "us-east-1",
                    ForcePathStyle = true
                });
        }

        public int GetPort()
        {
            return _container.GetMappedPublicPort(4566);
        }

        public async Task AddSecretString(string name, string value)
        {
            await _awsSecretsManagerClient.CreateSecretAsync(new CreateSecretRequest()
            {
                Name = name,
                SecretString = value
            });
        }

        public async Task AddSecretBinary(string name, byte[] value)
        {
            await _awsSecretsManagerClient.CreateSecretAsync(new CreateSecretRequest()
            {
                Name = name,
                SecretBinary = new MemoryStream(value, writable: false)
            });
        }

        public async Task DeleteSecret(string name)
        {
            await _awsSecretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest()
            {
                SecretId = name
            });
        }

        public async Task<string> CreateKek(string? alias)
        {
            var kek = await _awsKmsClient.CreateKeyAsync(new CreateKeyRequest());

            if (alias != null)
            {
                await _awsKmsClient.CreateAliasAsync(new CreateAliasRequest()
                {
                    TargetKeyId = kek.KeyMetadata.KeyId,
                    AliasName = alias
                });
            }
            
            return kek.KeyMetadata.KeyId;
        }

        public async Task DeleteKekAlias(string alias)
        {
            await _awsKmsClient.DeleteAliasAsync(new DeleteAliasRequest()
            {
                AliasName = alias
            });
        }

        public async Task CreateSesTemplate(string templateName, string subject, string body)
        {
            await _awsSesClient.CreateEmailTemplateAsync(new CreateEmailTemplateRequest
            {
                TemplateName = templateName,
                TemplateContent = new EmailTemplateContent
                {
                    Subject = subject,
                    Text = body
                }
            });
        }

        public async Task<List<SesMessage>> GetSesSentEmails()
        {
            using var client = new HttpClient()
            {
                BaseAddress = new Uri(_endpoint)
            };

            var response = await client.GetFromJsonAsync<SesResponse>("_ministack/ses/messages");
            if (response == null || response.Messages == null)
                return new List<SesMessage>();

            var result = new List<SesMessage>();
            foreach (var messages in response.Messages.Values)
            {
                result.AddRange(messages);
            }

            return result;
        }

        public async Task CreateBucket(string bucketName)
        {
            await _awsS3BucketClient.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucketName
            });
        }

        public async Task DeleteBucket(string bucketName)
        {
            await _awsS3BucketClient.DeleteBucketAsync(new DeleteBucketRequest()
            {
                BucketName = bucketName
            });
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        public record SesResponse(Dictionary<string, List<SesMessage>>? Messages);
        public record SesMessage(string? MessageId, string? Source, List<string> To, string? Subject, string? BodyText);
    }
}
