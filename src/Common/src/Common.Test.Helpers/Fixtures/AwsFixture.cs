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

        private string _endpoint = null!;
        private AmazonSecretsManagerClient _awsSecretsManagerClient = null!;
        private AmazonKeyManagementServiceClient _awsKmsClient = null!;
        private AmazonSimpleEmailServiceV2Client _awsSesClient = null!;
        private AmazonS3Client _awsS3BucketClient = null!;
        private AmazonElastiCacheClient _awsElastiCache = null!;
        private AmazonECSClient _awsEcsClient = null!;

        public async Task InitializeAsync()
        {
            _container = new ContainerBuilder("ministackorg/ministack:latest")
                .WithPortBinding(4566, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged("Running on"))
                .Build();

            await _container.StartAsync();

            // Configurations
            _endpoint = $"http://localhost:{GetMappedPort()}";

            _awsSecretsManagerClient = new AmazonSecretsManagerClient(AwsTestConstants.Credentials,
                new AmazonSecretsManagerConfig()
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = AwsTestConstants.Region
                });

            _awsKmsClient = new AmazonKeyManagementServiceClient(AwsTestConstants.Credentials,
                new AmazonKeyManagementServiceConfig()
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = AwsTestConstants.Region
                });

            _awsSesClient = new AmazonSimpleEmailServiceV2Client(AwsTestConstants.Credentials,
                new AmazonSimpleEmailServiceV2Config()
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = AwsTestConstants.Region
                });

            _awsS3BucketClient = new AmazonS3Client(AwsTestConstants.Credentials,
                new AmazonS3Config()
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = AwsTestConstants.Region,
                    ForcePathStyle = true
                });

            _awsElastiCache = new AmazonElastiCacheClient(AwsTestConstants.Credentials,
                new AmazonElastiCacheConfig()
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = AwsTestConstants.Region,
                });

            _awsEcsClient = new AmazonECSClient(AwsTestConstants.Credentials,
                new AmazonECSConfig()
                {
                    ServiceURL = _endpoint,
                    AuthenticationRegion = AwsTestConstants.Region,
                });

            await SES_VerifyEmailAsync(AwsTestConstants.SesFromEmail);
        }

        public override int GetMappedPort(int? port = null)
        {
            return _container.GetMappedPublicPort(4566);
        }

        //~ Begin - AWS Secrets Manager
        public async Task SecretsManager_AddStringAsync(string id, string value)
        {
            await _awsSecretsManagerClient.CreateSecretAsync(new CreateSecretRequest()
            {
                Name = id,
                SecretString = value
            });
        }

        public async Task SecretsManager_AddBinaryAsync(string id, byte[] value)
        {
            await using var memoryStream = new MemoryStream(value, writable: false);
            await _awsSecretsManagerClient.CreateSecretAsync(new CreateSecretRequest()
            {
                Name = id,
                SecretBinary = memoryStream
            });
        }

        public async Task<string> SecretsManager_GetStringAsync(string id)
        {
            var response = await _awsSecretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest()
            {
                SecretId = id
            });

            return response.SecretString;
        }

        public async Task<byte[]> SecretsManager_GetBinaryAsync(string id)
        {
            var response = await _awsSecretsManagerClient.GetSecretValueAsync(new GetSecretValueRequest()
            {
                SecretId = id
            });

            return response.SecretBinary.ToArray();
        }

        public async Task SecretsManager_DeleteAsync(string id)
        {
            await _awsSecretsManagerClient.DeleteSecretAsync(new DeleteSecretRequest()
            {
                SecretId = id,
                ForceDeleteWithoutRecovery = true
            });
        }
        //~ End

        //~ Begin - AWS KMS
        public async Task<string> KMS_CreateKekAsync(string? alias)
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

        public async Task KMS_DeleteAliasAsync(string alias)
        {
            await _awsKmsClient.DeleteAliasAsync(new DeleteAliasRequest()
            {
                AliasName = alias
            });
        }
        //~ End

        //~ Begin - AWS SES
        public async Task SES_VerifyEmailAsync(string email)
        {
            await _awsSesClient.CreateEmailIdentityAsync(new CreateEmailIdentityRequest()
            {
                EmailIdentity = email
            });
        }

        public async Task SES_CreateTemplateAsync(string templateName, string subject, string body)
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

        private record SesResponse(
            Dictionary<string, List<SesMessage>>? Messages
        );

        public async Task<List<SesMessage>> SES_GetSentEmailsAsync()
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
        //~ End

        //~ Begin - AWS S3 Bucket
        public async Task S3Bucket_CreateBucketAsync(string bucketName)
        {
            await _awsS3BucketClient.PutBucketAsync(new PutBucketRequest
            {
                BucketName = bucketName
            });
        }

        public async Task S3Bucket_PutObjectAsync(string bucketName, string key, byte[] data)
        {
            await _awsS3BucketClient.PutObjectAsync(new PutObjectRequest()
            {
                BucketName = bucketName,
                Key = key,
                InputStream = new MemoryStream(data, writable: false),
                AutoCloseStream = true
            });
        }

        public async Task<byte[]> S3Bucket_GetObjectAsync(string bucketName, string key)
        {
            using var response = await _awsS3BucketClient.GetObjectAsync(new GetObjectRequest()
            {
                BucketName = bucketName,
                Key = key
            });

            await using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            return memoryStream.ToArray();
        }

        public async Task<bool> S3Bucket_FileExistsAsync(string bucketName, string key)
        {
            try
            {
                await _awsS3BucketClient.GetObjectMetadataAsync(new GetObjectMetadataRequest()
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

        public async Task S3Bucket_DeleteBucketAsync(string bucketName)
        {
            await _awsS3BucketClient.DeleteBucketAsync(new DeleteBucketRequest()
            {
                BucketName = bucketName
            });
        }
        //~ End

        //~ Begin - ElastiCache
        public async Task ElastiCache_CreateUser(
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
        
        public async Task ElastiCache_CreateUserGroup(string groupName, string engine = "redis")
        {
            await _awsElastiCache.CreateUserGroupAsync(new CreateUserGroupRequest
            {
                UserGroupId = groupName,
                Engine = engine,
                UserIds = new List<string> { "default" }
            });
        }

        public async Task ElastiCache_ModifyUserGroup(string groupName, List<string>? usersToAdd, List<string>? usersToRemove)
        {
            await _awsElastiCache.ModifyUserGroupAsync(new ModifyUserGroupRequest
            {
                UserGroupId = groupName,
                UserIdsToAdd = usersToAdd ?? new List<string>(),
                UserIdsToRemove = usersToRemove ?? new List<string>()
            });
        }

        public async Task<User?> ElastiCache_GetUser(string username)
        {
            var usersResponse = await _awsElastiCache.DescribeUsersAsync(new DescribeUsersRequest()
            {
                UserId = username
            });

            return usersResponse.Users
                .Where(u => u.UserName == username)
                .FirstOrDefault();
        }

        public async Task ElastiCache_DeleteUser(string username)
        {
            await _awsElastiCache.DeleteUserAsync(new DeleteUserRequest()
            {
                UserId = username
            });
        }
        //~ End

        //~ Begin - ECS
        public async Task Ecs_CreateClusterAsync(string clusterName)
        {
            await _awsEcsClient.CreateClusterAsync(new Amazon.ECS.Model.CreateClusterRequest
            {
                ClusterName = clusterName
            });
        }

        public async Task Ecs_RegisterTaskDefiniton(string taskName)
        {
            await _awsEcsClient.RegisterTaskDefinitionAsync(new Amazon.ECS.Model.RegisterTaskDefinitionRequest
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

        public async Task Ecs_CreateServiceAsync(string clusterName, string taskName, string serviceName)
        {
            await _awsEcsClient.CreateServiceAsync(new Amazon.ECS.Model.CreateServiceRequest
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
        //~ End

        public async Task DisposeAsync()
        {
            await _container.DisposeAsync();
        }
    }
}
