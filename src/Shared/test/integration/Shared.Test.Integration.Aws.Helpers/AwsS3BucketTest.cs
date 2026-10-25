using System.Text;
using Shared.Aws.Helpers.Abstractions;
using Shared.Test.Generators;
using Shared.Test.Integration.Aws.Helpers.Collections;

namespace Shared.Test.Integration.Aws.Helpers
{
    [Collection(nameof(AwsCollection))]
    public class AwsS3BucketTest : IAsyncLifetime
    {
        private readonly AwsCollectionCluster _collectionCluster;

        private readonly string _bucketName = "aws-s3-test-bucket";

        public AwsS3BucketTest(AwsCollectionCluster collectionCluster)
        {
            _collectionCluster = collectionCluster;
        }

        public async Task InitializeAsync()
        {
            await _collectionCluster.AwsFixture.S3Bucket_CreateBucketAsync(_bucketName);
        }

        public Task DisposeAsync()
        {
            return Task.CompletedTask;
        }

        [Fact]
        public async Task UploadFileAsync_WhenStreamIsGiven_ShouldSaveItToBucketAndNotCloseStream()
        {
            // Arrange
            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await using var memoryStream = new MemoryStream(data);

            // Act
            await awsS3Bucket.UploadFileAsync(_bucketName, "upload", "stream.txt", memoryStream);

            // Assert
            var savedData = await _collectionCluster.AwsFixture.S3Bucket_GetObjectAsync(_bucketName, "upload/stream.txt");
            Assert.Equal(data, savedData);
            Assert.True(memoryStream.CanSeek);
            Assert.True(memoryStream.CanRead);
            Assert.True(memoryStream.CanWrite);
        }

        [Fact]
        public async Task UploadFileAsync_WhenBytesAreGiven_ShouldSaveItToBucket()
        {
            // Arrange
            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());

            // Act
            await awsS3Bucket.UploadFileAsync(_bucketName, "upload", "bytes.txt", data);

            // Assert
            var savedData = await _collectionCluster.AwsFixture.S3Bucket_GetObjectAsync(_bucketName, "upload/bytes.txt");
            Assert.Equal(data, savedData);
        }

        [Fact]
        public async Task DownloadFileAsync_WhenMethodIsCalled_ShouldDownloadFileAndReturnMemoryStream()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, "download/test.txt", data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await using var file = await awsS3Bucket.DownloadFileAsync(_bucketName, "download", "test.txt");

            // Assert
            Assert.Equal(data, file.ToArray());
        }

        [Fact]
        public async Task StreamFileAsync_WhenMethodIsCalled_ShouldReturnStreamConnectedToBucket()
        {
            // Arrange
            var value = StringGenerator.GenerateAlphanumeric();
            var data = Encoding.UTF8.GetBytes(value);
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, "download/stream.txt", data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await using var file = await awsS3Bucket.StreamFileAsync(_bucketName, "download", "stream.txt");

            // Assert
            Assert.Equal(value, await new StreamReader(file).ReadToEndAsync());
        }

        [Fact]
        public async Task DeleteFileAsync_WhenMethodIsCalled_ShouldDeleteFile()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, "delete/test.txt", data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await awsS3Bucket.DeleteFileAsync(_bucketName, "delete", "test.txt");

            // Assert
            Assert.False(await _collectionCluster.AwsFixture.S3Bucket_FileExistsAsync(_bucketName, "delete/test.txt"));
        }

        [Fact]
        public async Task DeleteFilesAsync_WhenFolderAndFileNamesAreGiven_ShouldDeleteFilesAndReturnFailedOnes()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, "delete/test2.txt", data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await awsS3Bucket.DeleteFilesAsync(_bucketName, "delete", ["test2.txt", "file.txt"]);

            // Assert
            Assert.False(await _collectionCluster.AwsFixture.S3Bucket_FileExistsAsync(_bucketName, "delete/test2.txt"));
        }

        [Fact]
        public async Task DeleteFilesAsync_WhenFilePathsAreGiven_ShouldDeleteFilesAndReturnFailedOnes()
        {
            // Arrange
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, "delete/test3.txt", data);
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, "delete2/test.txt", data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await awsS3Bucket.DeleteFilesAsync(_bucketName, ["delete/test3.txt", "delete2/test.txt", "file.txt"]);

            // Assert
            Assert.False(await _collectionCluster.AwsFixture.S3Bucket_FileExistsAsync(_bucketName, "delete/test3.txt"));
            Assert.False(await _collectionCluster.AwsFixture.S3Bucket_FileExistsAsync(_bucketName, "delete2/test.txt"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public async Task GetFileNamesUnderFolderAsync_WhenMethodIsCalled_ShouldReturnFileNames(int numberOfFiles)
        {
            // Arrange
            for (int i = 0; i < numberOfFiles; ++i)
            {
                var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
                await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, $"count/test-{i}.txt", data);
            }

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            var fileNames = await awsS3Bucket.GetFileNamesUnderFolderAsync(_bucketName, "count");

            // Assert
            Assert.Equal(numberOfFiles, fileNames.Count);
        }
    }
}
