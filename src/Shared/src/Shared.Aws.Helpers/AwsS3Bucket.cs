using Amazon.S3;
using Amazon.S3.Model;
using Shared.Aws.Helpers.Abstractions;

namespace Shared.Aws.Helpers
{
    internal class AwsS3Bucket : IAwsS3Bucket
    {
        private readonly int _maxBatchSize = 1000;

        private readonly IAmazonS3 _awsS3Bucket;

        public AwsS3Bucket(IAmazonS3 awsS3Bucket)
        {
            _awsS3Bucket = awsS3Bucket;
        }

        public async Task UploadFileAsync(
            string id,
            string folder,
            string fileName,
            Stream content,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            await _awsS3Bucket.PutObjectAsync(new PutObjectRequest()
            {
                BucketName = id,
                Key = $"{folder}/{fileName}",
                InputStream = content,
                ContentType = contentType,
                AutoCloseStream = false
            }, cancellationToken);
        }

        public async Task UploadFileAsync(
            string id,
            string folder,
            string fileName,
            byte[] content,
            string? contentType = null,
            CancellationToken cancellationToken = default)
        {
            await _awsS3Bucket.PutObjectAsync(new PutObjectRequest()
            {
                BucketName = id,
                Key = $"{folder}/{fileName}",
                InputStream = new MemoryStream(content, writable: false),
                ContentType = contentType,
                AutoCloseStream = true
            }, cancellationToken);
        }

        public async Task<MemoryStream> DownloadFileAsync(
            string id,
            string folder,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            using var response = await _awsS3Bucket.GetObjectAsync(new GetObjectRequest()
            {
                BucketName = id,
                Key = $"{folder}/{fileName}"
            }, cancellationToken);

            await using var memoryStream = new MemoryStream();
            await response.ResponseStream.CopyToAsync(memoryStream, cancellationToken);
            memoryStream.Position = 0;

            return memoryStream;
        }

        public async Task<Stream> StreamFileAsync(
            string id,
            string folder,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            var response = await _awsS3Bucket.GetObjectAsync(new GetObjectRequest()
            {
                BucketName = id,
                Key = $"{folder}/{fileName}"
            }, cancellationToken);

            return response.ResponseStream;
        }

        public async Task DeleteFileAsync(
            string id,
            string folder,
            string fileName,
            CancellationToken cancellationToken = default)
        {
            await _awsS3Bucket.DeleteObjectAsync(new DeleteObjectRequest()
            {
                BucketName = id,
                Key = $"{folder}/{fileName}"
            }, cancellationToken);
        }

        public async Task<IReadOnlyList<string>> DeleteFilesAsync(
            string id,
            string folder,
            IEnumerable<string> fileNames,
            CancellationToken cancellationToken = default)
        {
            var failedFiles = new List<string>();

            foreach (var fileBatch in fileNames.Chunk(_maxBatchSize))
            {
                var response = await _awsS3Bucket.DeleteObjectsAsync(new DeleteObjectsRequest()
                {
                    BucketName = id,
                    Objects = fileBatch.Select(f => new KeyVersion() { Key = $"{folder}/{f}" }).ToList(),
                    Quiet = true
                }, cancellationToken);

                if (response.DeleteErrors != null)
                {
                    foreach (var error in response.DeleteErrors)
                    {
                        failedFiles.Add(error.Key);
                    }
                }
            }

            return failedFiles;
        }

        public async Task<IReadOnlyList<string>> DeleteFilesAsync(
            string id,
            IEnumerable<string> filePaths,
            CancellationToken cancellationToken = default)
        {
            var failedFiles = new List<string>();

            foreach (var fileBatch in filePaths.Chunk(_maxBatchSize))
            {
                var response = await _awsS3Bucket.DeleteObjectsAsync(new DeleteObjectsRequest()
                {
                    BucketName = id,
                    Objects = fileBatch.Select(f => new KeyVersion() { Key = f }).ToList(),
                    Quiet = true
                }, cancellationToken);

                if (response.DeleteErrors != null)
                {
                    foreach (var error in response.DeleteErrors)
                    {
                        failedFiles.Add(error.Key);
                    }
                }
            }

            return failedFiles;
        }

        public async Task<IReadOnlyList<string>> GetFileNamesUnderFolderAsync(
            string id,
            string folder,
            CancellationToken cancellationToken = default)
        {
            var fileNames = new List<string>();
            string? token = null;
            while (true)
            {
                var page = await _awsS3Bucket.ListObjectsV2Async(new ListObjectsV2Request()
                {
                    BucketName = id,
                    Prefix = $"{folder}/",
                    ContinuationToken = token
                }, cancellationToken);

                if (page.S3Objects == null)
                    break;

                foreach (var s3Object in page.S3Objects)
                {
                    if (s3Object.Key.EndsWith('/'))
                        continue;

                    fileNames.Add(s3Object.Key.Substring(folder.Length + 1));
                }

                if (page.IsTruncated == null || !page.IsTruncated.Value)
                    break;

                token = page.NextContinuationToken;
            }

            return fileNames;
        }
    }
}
