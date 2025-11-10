namespace CSVProssessor.Application.Interfaces.Common;

public interface IBlobService
{
    /// <summary>
    ///     Tải file lên MinIO với tên và luồng dữ liệu cụ thể.
    /// </summary>
    Task UploadFileAsync(string fileName, Stream fileStream);

    /// <summary>
    ///     Lấy đường dẫn xem trước (preview URL) cho file đã upload.
    /// </summary>
    Task<string> GetPreviewUrlAsync(string fileName);

    /// <summary>
    ///     Lấy URL tải file có thời hạn từ MinIO (Presigned URL).
    /// </summary>
    Task<string> GetFileUrlAsync(string fileName);

    /// <summary>
    ///     Tải file từ MinIO về dưới dạng Stream.
    /// </summary>
    Task<Stream> DownloadFileAsync(string fileName);

    /// <summary>
    ///     Xóa file khỏi MinIO theo tên file.
    /// </summary>
    Task DeleteFileAsync(string fileName);

    /// <summary>
    ///     Thay thế ảnh cũ bằng ảnh mới: xóa ảnh cũ nếu có, upload ảnh mới và trả về preview URL.
    /// </summary>
    Task<string> ReplaceImageAsync(Stream newImageStream, string newImageName, string? oldImageUrl,
        string containerPrefix);

    /// <summary>
    ///     Zip tất cả file trong bucket và upload file zip lên folder "export" trong bucket
    /// </summary>
    /// <param name="outputBlobName">Tên file zip output (vd: "csv-export-20251110.zip")</param>
    /// <returns>SAS URL của file zip</returns>
    Task<string> ZipAndUploadAllAsync(string outputBlobName);

    /// <summary>
    ///     Upload single file vào folder "export" trong bucket và trả về SAS URL
    /// </summary>
    /// <param name="filePath">Đường dẫn file cần upload (tên file trong bucket)</param>
    /// <returns>SAS URL của file</returns>
    Task<string> UploadSingleFileAsync(string filePath);
}