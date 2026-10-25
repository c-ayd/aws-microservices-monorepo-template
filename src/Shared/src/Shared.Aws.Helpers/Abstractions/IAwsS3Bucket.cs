namespace Shared.Aws.Helpers.Abstractions
{
    public interface IAwsS3Bucket
    {
        Task UploadFileAsync(string bucketName, string folder, string fileName, Stream content, string? contentType = null, CancellationToken cancellationToken = default);
        Task UploadFileAsync(string bucketName, string folder, string fileName, byte[] content, string? contentType = null, CancellationToken cancellationToken = default);
        Task<MemoryStream> DownloadFileAsync(string bucketName, string folder, string fileName, CancellationToken cancellationToken = default);
        Task<Stream> StreamFileAsync(string bucketName, string folder, string fileName, CancellationToken cancellationToken = default);
        Task DeleteFileAsync(string bucketName, string folder, string fileName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> DeleteFilesAsync(string bucketName, string folder, IEnumerable<string> fileNames, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> DeleteFilesAsync(string bucketName, IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetFileNamesUnderFolderAsync(string bucketName, string folder, CancellationToken cancellationToken = default);
    }
}
