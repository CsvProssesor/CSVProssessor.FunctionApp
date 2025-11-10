namespace CSVProssessor.Application.Interfaces.Common;

public interface IBlobService
{
    Task UploadFileAsync(string fileName, Stream fileStream);

    Task<string> GetPreviewUrlAsync(string fileName);

    Task<string> GetFileUrlAsync(string fileName);

    Task<Stream> DownloadFileAsync(string fileName);

    Task DeleteFileAsync(string fileName);

    Task<string> ReplaceImageAsync(Stream newImageStream, string newImageName, string? oldImageUrl,
        string containerPrefix);

    Task<string> ZipAndUploadAllAsync(string outputBlobName);
    Task<string> UploadSingleFileAsync(string filePath);
}