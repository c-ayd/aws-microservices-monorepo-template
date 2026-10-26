using System.Text;
using Shared.Aws.Helpers.Abstractions;
using Shared.Test.Generators;
using Shared.Test.Integration.Aws.Helpers.Collections;

namespace Shared.Test.Integration.Aws.Helpers
{
    [Collection(nameof(AwsCollection))]
    public class AwsS3BucketTest : IAsyncLifetime
    {
        private const string _bucketName = "aws-s3-test-bucket";

        private readonly AwsCollectionCluster _collectionCluster;

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

            var folder = "upload";
            var fileName = "stream.txt";
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await using var memoryStream = new MemoryStream(data);

            // Act
            await awsS3Bucket.UploadFileAsync(_bucketName, folder, fileName, memoryStream);

            // Assert
            var savedData = await _collectionCluster.AwsFixture.S3Bucket_GetObjectAsync(
                _bucketName,
                key: $"{folder}/{fileName}");
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

            var folder = "upload";
            var fileName = "bytes.txt";
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());

            // Act
            await awsS3Bucket.UploadFileAsync(_bucketName, folder, fileName, data);

            var savedData = await _collectionCluster.AwsFixture.S3Bucket_GetObjectAsync(
                _bucketName,
                key: $"{folder}/{fileName}");
            Assert.Equal(data, savedData);
        }

        [Fact]
        public async Task DownloadFileAsync_WhenMethodIsCalled_ShouldDownloadFileAndReturnMemoryStream()
        {
            // Arrange
            var folder = "upload";
            var fileName = "test.txt";
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(
                _bucketName,
                key: $"{folder}/{fileName}",
                data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await using var file = await awsS3Bucket.DownloadFileAsync(_bucketName, folder, fileName);

            // Assert
            Assert.Equal(data, file.ToArray());
        }

        [Fact]
        public async Task StreamFileAsync_WhenMethodIsCalled_ShouldReturnStreamConnectedToBucket()
        {
            // Arrange
            var value = StringGenerator.GenerateAlphanumeric();
            var folder = "download";
            var fileName = "stream.txt";
            var data = Encoding.UTF8.GetBytes(value);
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(
                _bucketName,
                key: $"{folder}/{fileName}",
                data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await using var file = await awsS3Bucket.StreamFileAsync(_bucketName, folder, fileName);

            // Assert
            Assert.Equal(value, await new StreamReader(file).ReadToEndAsync());
        }

        [Fact]
        public async Task DeleteFileAsync_WhenMethodIsCalled_ShouldDeleteFile()
        {
            // Arrange
            var folder = "delete";
            var fileName = "test.txt";
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(
                _bucketName,
                key: $"{folder}/{fileName}",
                data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await awsS3Bucket.DeleteFileAsync(_bucketName, folder, fileName);

            // Assert
            Assert.False(await _collectionCluster.AwsFixture.S3Bucket_FileExistsAsync(
                _bucketName,
                key: $"{folder}/{fileName}"));
        }

        [Fact]
        public async Task DeleteFilesAsync_WhenFolderAndFileNamesAreGiven_ShouldDeleteFilesAndReturnFailedOnes()
        {
            // Arrange
            var folder = "delete";
            var fileName = "test2.txt";
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(
                _bucketName,
                key: $"{folder}/{fileName}",
                data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await awsS3Bucket.DeleteFilesAsync(
                _bucketName,
                folder,
                fileNames: [
                    fileName,
                    StringGenerator.GenerateAlphanumeric()
                ]);

            // Assert
            Assert.False(await _collectionCluster.AwsFixture.S3Bucket_FileExistsAsync(
                _bucketName,
                key: $"{folder}/{fileName}"));
        }

        [Fact]
        public async Task DeleteFilesAsync_WhenFilePathsAreGiven_ShouldDeleteFilesAndReturnFailedOnes()
        {
            // Arrange
            var key1 = "delete/test3.txt";
            var key2 = "delete2/test.txt";
            var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, key1, data);
            await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(_bucketName, key2, data);

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            await awsS3Bucket.DeleteFilesAsync(
                _bucketName,
                filePaths: [
                    key1,
                    key2,
                    StringGenerator.GenerateAlphanumeric()
                ]);

            // Assert
            Assert.False(await _collectionCluster.AwsFixture.S3Bucket_FileExistsAsync(_bucketName, key1));
            Assert.False(await _collectionCluster.AwsFixture.S3Bucket_FileExistsAsync(_bucketName, key2));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(2)]
        public async Task GetFileNamesUnderFolderAsync_WhenMethodIsCalled_ShouldReturnFileNames(int numberOfFiles)
        {
            // Arrange
            var folder = "count";
            for (int i = 0; i < numberOfFiles; ++i)
            {
                var data = Encoding.UTF8.GetBytes(StringGenerator.GenerateAlphanumeric());
                await _collectionCluster.AwsFixture.S3Bucket_PutObjectAsync(
                    _bucketName,
                    key: $"{folder}/test-{i}.txt",
                    data);
            }

            var awsS3Bucket = _collectionCluster.WebApiFixture.GetService<IAwsS3Bucket>();

            // Act
            var fileNames = await awsS3Bucket.GetFileNamesUnderFolderAsync(_bucketName, folder);

            // Assert
            Assert.Equal(numberOfFiles, fileNames.Count);
        }
    }
}
