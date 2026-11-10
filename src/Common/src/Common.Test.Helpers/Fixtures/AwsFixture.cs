using System.Net.Http.Json;
using Amazon.KeyManagementService;
using Amazon.KeyManagementService.Model;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Amazon.SimpleEmailV2;
using Amazon.SimpleEmailV2.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Common.Aws.Helpers.Test;
using Common.Test.Helpers.Fixtures.SeedWork;
using Common.Test.Helpers.Structures;
using Amazon.ECS;
using Amazon.ElastiCache;
using Amazon.ElastiCache.Model;

namespace Common.Test.Helpers.Fixtures
{
    /// <summary>
    /// Is an AWS emulator for test cases requiring AWS.
    /// </summary>
    public class AwsFixture : ContainerFixtureBase
    {
        protected override IContainer Container => _container;
        private IContainer _container = null!;
        
        public SecretsManager AwsSecretsManager { get; private set; } = null!;
        public Kms AwsKms { get; private set; } = null!;
        public Ses AwsSes { get; private set; } = null!;
        public S3Bucket AwsS3Bucket { get; private set; } = null!;
        public ElastiCache AwsElastiCache { get; private set; } = null!;
        public Ecs AwsEcs { get; private set; } = null!;

        public async Task InitializeAsync()
        {
            _container = new ContainerBuilder("ministackorg/ministack:latest")
                .WithPortBinding(4566, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Running on"))
                .Build();

            await _container.StartAsync();

            // Configurations
            var serviceUrl = $"http://localhost:{GetMappedPort()}";

            AwsSecretsManager = new SecretsManager(serviceUrl);
            AwsKms = new Kms(serviceUrl);
            AwsSes = new Ses(serviceUrl);
            AwsS3Bucket = new S3Bucket(serviceUrl);
            AwsElastiCache = new ElastiCache(serviceUrl);
            AwsEcs = new Ecs(serviceUrl);

            await AwsSes.VerifyEmailAsync(AwsTestConstants.SesFromEmail);
        }

        public override int GetMappedPort(int? port = null)
        {
            return _container.GetMappedPublicPort(4566);
        }

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }

        //~ Begin - AWS Services

        public class SecretsManager
        {
            private AmazonSecretsManagerClient _awsSecretsManager;

            public SecretsManager(string serviceUrl)
            {
                _awsSecretsManager = new AmazonSecretsManagerClient(
                    AwsTestConstants.Credentials,
                    new AmazonSecretsManagerConfig()
                    {
                        ServiceURL = serviceUrl,
                        AuthenticationRegion = AwsTestConstants.Region
                    });
            }

            public async Task AddStringAsync(string id, string value)
            {
                await _awsSecretsManager.CreateSecretAsync(new CreateSecretRequest()
                {
                    Name = id,
                    SecretString = value
                });
            }

            public async Task AddBinaryAsync(string id, byte[] value)
            {
                await using var memoryStream = new MemoryStream(value, writable: false);
                await _awsSecretsManager.CreateSecretAsync(new CreateSecretRequest()
                {
                    Name = id,
                    SecretBinary = memoryStream
                });
            }

            public async Task<string> GetStringAsync(string id)
            {
                var response = await _awsSecretsManager.GetSecretValueAsync(new GetSecretValueRequest()
                {
                    SecretId = id
                });

                return response.SecretString;
            }

            public async Task<byte[]> GetBinaryAsync(string id)
            {
                var response = await _awsSecretsManager.GetSecretValueAsync(new GetSecretValueRequest()
                {
                    SecretId = id
                });

                return response.SecretBinary.ToArray();
            }

            public async Task DeleteAsync(string id)
            {
                await _awsSecretsManager.DeleteSecretAsync(new DeleteSecretRequest()
                {
                    SecretId = id,
                    ForceDeleteWithoutRecovery = true
                });
            }
        }

        public class Kms
        {
            private AmazonKeyManagementServiceClient _awsKms;

            public Kms(string serviceUrl)
            {
                _awsKms = new AmazonKeyManagementServiceClient(
                    AwsTestConstants.Credentials,
                    new AmazonKeyManagementServiceConfig()
                    {
                        ServiceURL = serviceUrl,
                        AuthenticationRegion = AwsTestConstants.Region
                    });
            }

            public async Task<string> CreateKekAsync(string? alias)
            {
                var kek = await _awsKms.CreateKeyAsync(new CreateKeyRequest());

                if (alias != null)
                {
                    await _awsKms.CreateAliasAsync(new CreateAliasRequest()
                    {
                        TargetKeyId = kek.KeyMetadata.KeyId,
                        AliasName = alias
                    });
                }

                return kek.KeyMetadata.KeyId;
            }

            public async Task DeleteAliasAsync(string alias)
            {
                await _awsKms.DeleteAliasAsync(new DeleteAliasRequest()
                {
                    AliasName = alias
                });
            }
        }

        public class Ses
        {
            private string _serviceUrl;
            private AmazonSimpleEmailServiceV2Client _awsSes;

            public Ses(string serviceUrl)
            {
                _serviceUrl = serviceUrl;
                _awsSes = new AmazonSimpleEmailServiceV2Client(
                    AwsTestConstants.Credentials,
                    new AmazonSimpleEmailServiceV2Config()
                    {
                        ServiceURL = serviceUrl,
                        AuthenticationRegion = AwsTestConstants.Region
                    });
            }

            public async Task VerifyEmailAsync(string email)
            {
                await _awsSes.CreateEmailIdentityAsync(new CreateEmailIdentityRequest()
                {
                    EmailIdentity = email
                });
            }

            public async Task CreateTemplateAsync(string templateName, string subject, string body)
            {
                await _awsSes.CreateEmailTemplateAsync(new CreateEmailTemplateRequest
                {
                    TemplateName = templateName,
                    TemplateContent = new EmailTemplateContent
                    {
                        Subject = subject,
                        Text = body
                    }
                });
            }

            public async Task<List<SesMessage>> GetSentEmailsAsync()
            {
                using var client = new HttpClient()
                {
                    BaseAddress = new Uri(_serviceUrl)
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

            private record SesResponse(
                Dictionary<string, List<SesMessage>>? Messages
            );
        }

        public class S3Bucket
        {
            private AmazonS3Client _awsS3Bucket;

            public S3Bucket(string serviceUrl)
            {
                _awsS3Bucket = new AmazonS3Client(
                    AwsTestConstants.Credentials,
                    new AmazonS3Config()
                    {
                        ServiceURL = serviceUrl,
                        AuthenticationRegion = AwsTestConstants.Region,
                        ForcePathStyle = true
                    });
            }

            public async Task CreateBucketAsync(string bucketName)
            {
                await _awsS3Bucket.PutBucketAsync(new PutBucketRequest
                {
                    BucketName = bucketName
                });
            }

            public async Task PutObjectAsync(string bucketName, string key, byte[] data)
            {
                await _awsS3Bucket.PutObjectAsync(new PutObjectRequest()
                {
                    BucketName = bucketName,
                    Key = key,
                    InputStream = new MemoryStream(data, writable: false),
                    AutoCloseStream = true
                });
            }

            public async Task<byte[]> GetObjectAsync(string bucketName, string key)
            {
                using var response = await _awsS3Bucket.GetObjectAsync(new GetObjectRequest()
                {
                    BucketName = bucketName,
                    Key = key
                });

                await using var memoryStream = new MemoryStream();
                await response.ResponseStream.CopyToAsync(memoryStream);
                memoryStream.Position = 0;

                return memoryStream.ToArray();
            }

            public async Task<bool> FileExistsAsync(string bucketName, string key)
            {
                try
                {
                    await _awsS3Bucket.GetObjectMetadataAsync(new GetObjectMetadataRequest()
                    {
                        BucketName = bucketName,
                        Key = key
                    });

                    return true;
                }
                catch
                {
                    return false;
                }
            }

            public async Task DeleteBucketAsync(string bucketName)
            {
                await _awsS3Bucket.DeleteBucketAsync(new DeleteBucketRequest()
                {
                    BucketName = bucketName
                });
            }
        }

        public class ElastiCache
        {
            private AmazonElastiCacheClient _awsElastiCache;

            public ElastiCache(string serviceUrl)
            {
                _awsElastiCache = new AmazonElastiCacheClient(
                    AwsTestConstants.Credentials,
                    new AmazonElastiCacheConfig()
                    {
                        ServiceURL = serviceUrl,
                        AuthenticationRegion = AwsTestConstants.Region,
                    });
            }

            public async Task CreateUser(
                string username,
                string password,
                string accessString = "~* +@all -@dangerous",
                string engine = "redis")
            {
                await _awsElastiCache.CreateUserAsync(new CreateUserRequest()
                {
                    UserId = username,
                    UserName = username,
                    Engine = engine,
                    AccessString = accessString,
                    Passwords = new List<string>() { password }
                });
            }

            public async Task CreateUserGroup(string groupId, string engine = "redis")
            {
                await _awsElastiCache.CreateUserGroupAsync(new CreateUserGroupRequest
                {
                    UserGroupId = groupId,
                    Engine = engine,
                    UserIds = new List<string> { "default" }
                });
            }

            public async Task ModifyUserGroup(string groupId, List<string>? usersToAdd, List<string>? usersToRemove)
            {
                await _awsElastiCache.ModifyUserGroupAsync(new ModifyUserGroupRequest
                {
                    UserGroupId = groupId,
                    UserIdsToAdd = usersToAdd ?? new List<string>(),
                    UserIdsToRemove = usersToRemove ?? new List<string>()
                });
            }

            public async Task<User?> GetUser(string username)
            {
                try
                {
                    var usersResponse = await _awsElastiCache.DescribeUsersAsync(new DescribeUsersRequest()
                    {
                        UserId = username
                    });

                    return usersResponse.Users
                        .Where(u => u.UserName == username)
                        .FirstOrDefault();
                }
                catch (UserNotFoundException)
                {
                    return null;
                }
            }

            public async Task DeleteUser(string username)
            {
                await _awsElastiCache.DeleteUserAsync(new DeleteUserRequest()
                {
                    UserId = username
                });
            }
        }

        public class Ecs
        {
            private AmazonECSClient _awsEcs;

            public Ecs(string serviceUrl)
            {
                _awsEcs = new AmazonECSClient(
                    AwsTestConstants.Credentials,
                    new AmazonECSConfig()
                    {
                        ServiceURL = serviceUrl,
                        AuthenticationRegion = AwsTestConstants.Region,
                    });
            }

            public async Task CreateClusterAsync(string clusterName)
            {
                await _awsEcs.CreateClusterAsync(new Amazon.ECS.Model.CreateClusterRequest
                {
                    ClusterName = clusterName
                });
            }

            public async Task RegisterTaskDefiniton(string taskName)
            {
                await _awsEcs.RegisterTaskDefinitionAsync(new Amazon.ECS.Model.RegisterTaskDefinitionRequest
                {
                    Family = taskName,
                    NetworkMode = NetworkMode.Awsvpc,
                    RequiresCompatibilities = [Compatibility.FARGATE],
                    Cpu = "256",
                    Memory = "512",
                    ContainerDefinitions = [
                        new Amazon.ECS.Model.ContainerDefinition
                        {
                            Name = "app",
                            Image = "image"
                        }
                    ]
                });
            }

            public async Task CreateServiceAsync(string clusterName, string taskName, string serviceName)
            {
                await _awsEcs.CreateServiceAsync(new Amazon.ECS.Model.CreateServiceRequest
                {
                    Cluster = clusterName,
                    TaskDefinition = taskName,
                    ServiceName = serviceName,
                    LaunchType = LaunchType.FARGATE,
                    DesiredCount = 0,
                    NetworkConfiguration = new Amazon.ECS.Model.NetworkConfiguration
                    {
                        AwsvpcConfiguration = new Amazon.ECS.Model.AwsVpcConfiguration
                        {
                            Subnets = ["subnet-123"]
                        }
                    }
                });
            }
        }

        //~ End
    }
}
