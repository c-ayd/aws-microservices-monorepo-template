namespace Common.Aws.Helpers.Abstractions
{
    /// <summary>
    /// Provides methods to interact with AWS S3 Bucket.
    /// </summary>
    public interface IAwsS3Bucket
    {
        Task UploadFileAsync(string bucketName, string folder, string fileName, Stream content, string? contentType = null, CancellationToken cancellationToken = default);
        Task UploadFileAsync(string bucketName, string folder, string fileName, byte[] content, string? contentType = null, CancellationToken cancellationToken = default);
        Task<MemoryStream> DownloadFileAsync(string bucketName, string folder, string fileName, CancellationToken cancellationToken = default);
        Task<Stream> StreamFileAsync(string bucketName, string folder, string fileName, CancellationToken cancellationToken = default);
        Task DeleteFileAsync(string bucketName, string folder, string fileName, CancellationToken cancellationToken = default);
        Task<List<string>> DeleteFilesAsync(string bucketName, string folder, IEnumerable<string> fileNames, CancellationToken cancellationToken = default);
        Task<List<string>> DeleteFilesAsync(string bucketName, IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
        Task<List<string>> GetFileNamesUnderFolderAsync(string bucketName, string folder, CancellationToken cancellationToken = default);
    }
}
