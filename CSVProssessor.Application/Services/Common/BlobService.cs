using System.IO.Compression;
using System.Text;
using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Application.Utils;
using CSVProssessor.Infrastructure.Interfaces;
using Minio;
using Minio.DataModel.Args;
using Minio.Exceptions;

namespace CSVProssessor.Application.Services.Common;

public class BlobService : IBlobService
{
    private readonly string? _accessKey;
    private readonly string _bucketName = "csvfiles";
    private readonly string? _endpoint;
    private readonly ILoggerService _logger;
    private readonly string? _secretKey;
    private IMinioClient? _minioClient;

    public BlobService(ILoggerService logger)
    {
        _logger = logger;

        _endpoint = Environment.GetEnvironmentVariable("MINIO_ENDPOINT")?.Trim();
        _accessKey = Environment.GetEnvironmentVariable("MINIO_ACCESS_KEY")?.Trim();
        _secretKey = Environment.GetEnvironmentVariable("MINIO_SECRET_KEY")?.Trim();

        if (string.IsNullOrWhiteSpace(_endpoint) || string.IsNullOrWhiteSpace(_accessKey) ||
            string.IsNullOrWhiteSpace(_secretKey))
            throw new InvalidOperationException(
                "MinIO configuration is missing. Please set MINIO_ENDPOINT, MINIO_ACCESS_KEY, and MINIO_SECRET_KEY environment variables.");
    }

    public async Task UploadFileAsync(string fileName, Stream fileStream)
    {
        try
        {
            var client = GetOrCreateClient();

            // Kiểm tra bucket tồn tại, nếu chưa thì tạo mới
            var beArgs = new BucketExistsArgs().WithBucket(_bucketName);
            var found = await client.BucketExistsAsync(beArgs);

            if (!found)
            {
                var mbArgs = new MakeBucketArgs().WithBucket(_bucketName);
                await client.MakeBucketAsync(mbArgs);
            }

            // Lấy content type dựa trên phần mở rộng của file
            var contentType = GetContentType(fileName);

            var putObjectArgs = new PutObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName)
                .WithStreamData(fileStream)
                .WithObjectSize(fileStream.Length)
                .WithContentType(contentType);

            await client.PutObjectAsync(putObjectArgs);
        }
        catch (MinioException minioEx)
        {
            _logger.Error($"MinIO Error during upload: {minioEx.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error during file upload: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetPreviewUrlAsync(string fileName)
    {
        // Biến MINIO_HOST phải trỏ tới reverse proxy HTTPS, vd: https://minio.fpt-devteam.fun
        var minioHost = Environment.GetEnvironmentVariable("MINIO_HOST") ?? "https://minio.fpt-devteam.fun";

        // Sử dụng Base64 encoding thay vì URL encoding để phù hợp với định dạng API
        var base64File = Convert.ToBase64String(Encoding.UTF8.GetBytes(fileName));

        // URL được định dạng đúng với API reverse proxy
        var previewUrl =
            $"{minioHost}/api/v1/buckets/{_bucketName}/objects/download?preview=true&prefix={base64File}&version_id=null";
        _logger.Info($"Preview URL generated: {previewUrl}");

        return previewUrl;
    }

    public async Task<string> GetFileUrlAsync(string fileName)
    {
        try
        {
            var client = GetOrCreateClient();
            var args = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName)
                .WithExpiry(7 * 24 * 60 * 60);

            var fileUrl = await client.PresignedGetObjectAsync(args);

            // Replace internal MinIO URL with public URL for external access
            var minioPublicUrl = Environment.GetEnvironmentVariable("MINIO_PUBLIC_URL");
            if (!string.IsNullOrWhiteSpace(minioPublicUrl))
                // Replace minio:9000 with localhost:9000 (or configured public URL)
                fileUrl = fileUrl.Replace("minio:9000",
                    minioPublicUrl.Replace("http://", "").Replace("https://", ""));

            _logger.Success($"Presigned file URL generated: {fileUrl}");
            return fileUrl;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error generating file URL: {ex.Message}");
            throw;
        }
    }

    public async Task<Stream> DownloadFileAsync(string fileName)
    {
        _logger.Info($"Downloading file: {fileName}");

        try
        {
            var client = GetOrCreateClient();
            var memoryStream = new MemoryStream();
            var getObjectArgs = new GetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName)
                .WithCallbackStream(async stream => { await stream.CopyToAsync(memoryStream); });

            await client.GetObjectAsync(getObjectArgs);
            memoryStream.Position = 0; // Reset stream position to beginning
            _logger.Success($"File '{fileName}' downloaded successfully. Size: {memoryStream.Length} bytes");
            return memoryStream;
        }
        catch (MinioException minioEx)
        {
            _logger.Error($"MinIO Error during download: {minioEx.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error during file download: {ex.Message}");
            throw;
        }
    }

    public async Task DeleteFileAsync(string fileName)
    {
        _logger.Info($"Deleting file: {fileName}");

        try
        {
            var client = GetOrCreateClient();
            var removeObjectArgs = new RemoveObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(fileName);

            await client.RemoveObjectAsync(removeObjectArgs);
            _logger.Success($"File '{fileName}' deleted successfully.");
        }
        catch (MinioException minioEx)
        {
            _logger.Error($"MinIO Error during delete: {minioEx.Message}");
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error($"Unexpected error during file delete: {ex.Message}");
            throw;
        }
    }

    public async Task<string> ReplaceImageAsync(Stream newImageStream, string originalFileName, string? oldImageUrl,
        string containerPrefix)
    {
        try
        {
            // Xóa ảnh cũ nếu có
            if (!string.IsNullOrWhiteSpace(oldImageUrl))
                try
                {
                    var oldFileName = Path.GetFileName(new Uri(oldImageUrl).LocalPath);
                    var fullOldPath = $"{containerPrefix}/{oldFileName}";
                    await DeleteFileAsync(fullOldPath);
                    _logger.Info($"[ReplaceImageAsync] Deleted old image: {fullOldPath}");
                }
                catch (Exception ex)
                {
                    _logger.Warn($"[ReplaceImageAsync] Failed to delete old image: {ex.Message}");
                }

            // Upload ảnh mới
            var newFileName = $"{containerPrefix}/{Guid.NewGuid()}{Path.GetExtension(originalFileName)}";
            _logger.Info($"[ReplaceImageAsync] Uploading new image: {newFileName}");

            await UploadFileAsync(newFileName, newImageStream);

            var previewUrl = await GetPreviewUrlAsync(newFileName);
            _logger.Success($"[ReplaceImageAsync] Uploaded and generated preview URL: {previewUrl}");
            return previewUrl;
        }
        catch (Exception ex)
        {
            _logger.Error($"[ReplaceImageAsync] Error occurred: {ex.Message}");
            throw ErrorHelper.Internal("Lỗi khi xử lý ảnh.");
        }
    }

    /// <summary>
    ///     Zip tất cả file trong bucket và upload file zip lên bucket trong folder "export"
    /// </summary>
    /// <param name="outputBlobName">Tên file zip output (vd: "export-20251110.zip")</param>
    /// <returns>SAS URL của file zip</returns>
    public async Task<string> ZipAndUploadAllAsync(string outputBlobName)
    {
        try
        {
            _logger.Info("Starting to zip all files from bucket");

            var client = GetOrCreateClient();

            // Đường dẫn đầy đủ cho file zip trong folder export
            var exportFolderPath = "export";
            var zipFilePath = $"{exportFolderPath}/{outputBlobName}";

            // Tạo memory stream để lưu trữ zip
            using (var zipStream = new MemoryStream())
            {
                using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
                {
                    // List all objects trong bucket
                    var listArgs = new ListObjectsArgs()
                        .WithBucket(_bucketName)
                        .WithRecursive(true);

                    var observable = client.ListObjectsEnumAsync(listArgs);

                    var fileCount = 0;
                    await foreach (var obj in observable)
                    {
                        // Skip folder và file trong export folder
                        if (obj.IsDir || obj.Key.StartsWith(exportFolderPath))
                            continue;

                        try
                        {
                            // Download file
                            var fileStream = new MemoryStream();
                            var getObjectArgs = new GetObjectArgs()
                                .WithBucket(_bucketName)
                                .WithObject(obj.Key)
                                .WithCallbackStream(async stream => { await stream.CopyToAsync(fileStream); });

                            await client.GetObjectAsync(getObjectArgs);
                            fileStream.Position = 0;

                            // Thêm vào zip archive
                            var entry = archive.CreateEntry(obj.Key, CompressionLevel.Optimal);
                            using (var entryStream = entry.Open())
                            {
                                await fileStream.CopyToAsync(entryStream);
                            }

                            fileCount++;
                            _logger.Info($"Added file to zip: {obj.Key}");
                        }
                        catch (Exception ex)
                        {
                            _logger.Warn($"Failed to add file {obj.Key} to zip: {ex.Message}");
                        }
                    }

                    _logger.Info($"Total files added to zip: {fileCount}");
                }

                // Upload zip file lên bucket
                zipStream.Position = 0;
                await UploadFileAsync(zipFilePath, zipStream);
                _logger.Success($"Zip file uploaded: {zipFilePath}");
            }

            // Tạo SAS URL cho file zip
            var sasUrl = await GeneratePresignedUrlAsync(zipFilePath);
            _logger.Success($"SAS URL generated for zip file: {sasUrl}");

            return sasUrl;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error in ZipAndUploadAllAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Upload single file vào folder "export" trong bucket và trả về SAS URL
    /// </summary>
    /// <param name="filePath">Đường dẫn file cần upload (tên file trong bucket)</param>
    /// <returns>SAS URL của file</returns>
    public async Task<string> UploadSingleFileAsync(string filePath)
    {
        try
        {
            _logger.Info($"Uploading single file to export folder: {filePath}");

            var client = GetOrCreateClient();

            // Kiểm tra file tồn tại trong bucket
            var fileStream = new MemoryStream();
            try
            {
                var getObjectArgs = new GetObjectArgs()
                    .WithBucket(_bucketName)
                    .WithObject(filePath)
                    .WithCallbackStream(async stream => { await stream.CopyToAsync(fileStream); });

                await client.GetObjectAsync(getObjectArgs);
                fileStream.Position = 0;
            }
            catch (ObjectNotFoundException)
            {
                _logger.Error($"File not found in bucket: {filePath}");
                throw ErrorHelper.NotFound($"File '{filePath}' không tồn tại trong bucket");
            }

            // Đặt đường dẫn đầy đủ trong folder export
            var exportFolderPath = "export";
            var fileName = Path.GetFileName(filePath);
            var exportFilePath = $"{exportFolderPath}/{fileName}";

            // Upload file vào folder export
            await UploadFileAsync(exportFilePath, fileStream);
            _logger.Success($"File uploaded to export folder: {exportFilePath}");

            // Tạo SAS URL cho file
            var sasUrl = await GeneratePresignedUrlAsync(exportFilePath);
            _logger.Success($"SAS URL generated for file: {sasUrl}");

            return sasUrl;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error in UploadSingleFileAsync: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Generate Presigned URL (SAS URL) cho file
    /// </summary>
    private async Task<string> GeneratePresignedUrlAsync(string filePath)
    {
        try
        {
            var client = GetOrCreateClient();
            var args = new PresignedGetObjectArgs()
                .WithBucket(_bucketName)
                .WithObject(filePath)
                .WithExpiry(7 * 24 * 60 * 60); // 7 days

            var sasUrl = await client.PresignedGetObjectAsync(args);

            // Replace internal MinIO URL with public URL if configured
            var minioPublicUrl = Environment.GetEnvironmentVariable("MINIO_PUBLIC_URL");
            if (!string.IsNullOrWhiteSpace(minioPublicUrl))
            {
                var publicUrlBase = minioPublicUrl.Replace("http://", "").Replace("https://", "").TrimEnd('/');
                sasUrl = sasUrl.Replace("minio:9000", publicUrlBase);
            }

            return sasUrl;
        }
        catch (Exception ex)
        {
            _logger.Error($"Error generating presigned URL: {ex.Message}");
            throw;
        }
    }

    private IMinioClient GetOrCreateClient()
    {
        if (_minioClient != null)
            return _minioClient;

        var cleanEndpoint = _endpoint!
            .Replace("https://", "", StringComparison.OrdinalIgnoreCase)
            .Replace("http://", "", StringComparison.OrdinalIgnoreCase)
            .Trim();

        _minioClient = new MinioClient()
            .WithEndpoint(cleanEndpoint)
            .WithCredentials(_accessKey!, _secretKey!)
            .WithSSL(false)
            .Build();

        return _minioClient;
    }

    private string GetContentType(string fileName)
    {
        _logger.Info($"Determining content type for file: {fileName}");
        var extension = Path.GetExtension(fileName)?.ToLower();

        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".pdf" => "application/pdf",
            ".mp4" => "video/mp4",
            _ => "application/octet-stream" // fallback nếu định dạng không rõ
        };
    }
}