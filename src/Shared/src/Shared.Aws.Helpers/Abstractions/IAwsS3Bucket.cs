namespace Shared.Aws.Helpers.Abstractions
{
    public interface IAwsS3Bucket
    {
        Task UploadFileAsync(string id, string folder, string fileName, Stream content, string? contentType = null, CancellationToken cancellationToken = default);
        Task UploadFileAsync(string id, string folder, string fileName, byte[] content, string? contentType = null, CancellationToken cancellationToken = default);
        Task<MemoryStream> DownloadFileAsync(string id, string folder, string fileName, CancellationToken cancellationToken = default);
        Task<Stream> StreamFileAsync(string id, string folder, string fileName, CancellationToken cancellationToken = default);
        Task DeleteFileAsync(string id, string folder, string fileName, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> DeleteFilesAsync(string id, string folder, IEnumerable<string> fileNames, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> DeleteFilesAsync(string id, IEnumerable<string> filePaths, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<string>> GetFileNamesUnderFolderAsync(string id, string folder, CancellationToken cancellationToken = default);
    }
}
