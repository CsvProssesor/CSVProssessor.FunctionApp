using CSVProssessor.Application.Interfaces;
using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Application.Utils;
using CSVProssessor.Domain.DTOs.CsvJobDTOs;
using CSVProssessor.Domain.Entities;
using CSVProssessor.Domain.Enums;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using System.IO.Compression;
using System.Text.Json;

namespace CSVProssessor.Application.Services
{
    public class CsvService : ICsvService
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IBlobService _blobService;
        private readonly IRabbitMqService _rabbitMqService;

        public CsvService(IUnitOfWork unitOfWork, IBlobService blobService, IRabbitMqService rabbitMqService)
        {
            _unitOfWork = unitOfWork;
            _blobService = blobService;
            _rabbitMqService = rabbitMqService;
        }

        public async Task<ImportCsvResponseDto> ImportCsvAsync(IFormFile file)
        {
            // Kiểm tra input hợp lệ
            if (file == null)
                throw ErrorHelper.BadRequest("File không được để trống.");

            if (file.Length == 0)
                throw ErrorHelper.BadRequest("File không được rỗng.");

            if (string.IsNullOrWhiteSpace(file.FileName))
                throw ErrorHelper.BadRequest("Tên file không được để trống.");

            // Generate unique file name to avoid conflicts
            var originalFileName = file.FileName;
            var fileExtension = Path.GetExtension(originalFileName);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(originalFileName);
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var uniqueId = Guid.NewGuid().ToString("N").Substring(0, 8); // Short GUID (8 chars)
            var uniqueFileName = $"{fileNameWithoutExtension}_{timestamp}_{uniqueId}{fileExtension}";

            // Đọc file stream từ IFormFile
            using var stream = file.OpenReadStream();

            // 1. tải file lên MinIO blob storage với unique name
            await _blobService.UploadFileAsync(uniqueFileName, stream);

            // 2. ghi nhận job vào database
            var jobId = Guid.NewGuid();
            var csvJob = new CsvJob
            {
                Id = jobId,
                FileName = uniqueFileName,
                OriginalFileName = originalFileName,
                Type = CsvJobType.Import,
                Status = CsvJobStatus.Pending,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _unitOfWork.CsvJobs.AddAsync(csvJob);
            await _unitOfWork.SaveChangesAsync();

            // 3. Prepare and publish message to RabbitMQ queue
            var message = new CsvImportMessage
            {
                JobId = jobId,
                FileName = uniqueFileName,
                UploadedAt = DateTime.UtcNow
            };

            // Publish message vào queue cho background service xử lý
            await _rabbitMqService.PublishAsync("csv-import-queue", message);

            // Return response DTO
            return new ImportCsvResponseDto
            {
                JobId = jobId,
                FileName = uniqueFileName,
                UploadedAt = DateTime.UtcNow,
                Status = csvJob.Status.ToString(),
                Message = $"File CSV đã được tải lên thành công với tên: {uniqueFileName}. Background service sẽ xử lý trong thời gian sớm nhất."
            };
        }
        
        public async Task SaveCsvRecordsAsync(Guid jobId, string fileName)
        {
            // 1. Download file from MinIO
            using var fileStream = await _blobService.DownloadFileAsync(fileName);

            // 2. Parse CSV file
            var records = await ParseCsvAsync(jobId, fileName, fileStream);

            if (records == null || records.Count == 0)
                throw ErrorHelper.BadRequest("Không có records để lưu vào database.");

            // 3. Add records to database in batches to avoid timeout
            const int batchSize = 50; // Insert 50 records at a time
            for (int i = 0; i < records.Count; i += batchSize)
            {
                var batch = records.Skip(i).Take(batchSize).ToList();
                foreach (var record in batch)
                {
                    await _unitOfWork.CsvRecords.AddAsync(record);
                }
                // Save each batch
                await _unitOfWork.SaveChangesAsync();
            }

            // 4. Update job status to Completed
            var csvJob = await _unitOfWork.CsvJobs.FirstOrDefaultAsync(x => x.Id == jobId);
            if (csvJob != null)
            {
                csvJob.Status = CsvJobStatus.Completed;
                csvJob.UpdatedAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();
            }
        }
        
        public async Task PublishCsvChangeAsync(string changeType, object changedDocument)
        {
            var message = new
            {
                ChangeType = changeType,
                Document = changedDocument,
                PublishedAt = DateTime.UtcNow
            };

            await _rabbitMqService.PublishToTopicAsync("csv-changes-topic", message);

            Console.WriteLine($"[CsvService] Published change '{changeType}' at {DateTime.UtcNow:u}");
        }

        public async Task LogCsvChangesAsync()
        {
            await _rabbitMqService.SubscribeToTopicAsync("csv-changes-topic", async (message) =>
            {
                Console.WriteLine("[CsvService-Logger] Received message:");
                Console.WriteLine(message); 
                await Task.CompletedTask;
            });
        }

        public async Task<ListCsvFilesResponseDto> ListAllCsvFilesAsync()
        {
            // 1. Query all CSV import jobs from database
            var csvJobs = await _unitOfWork.CsvJobs.GetAllAsync(x =>
                x.Type == CsvJobType.Import && !x.IsDeleted
            );

            if (csvJobs == null || csvJobs.Count == 0)
            {
                return new ListCsvFilesResponseDto
                {
                    TotalFiles = 0,
                    Files = new List<CsvFileInfoDto>(),
                    GeneratedAt = DateTime.UtcNow,
                    Message = "Không có file CSV nào trong hệ thống."
                };
            }

            // 2. Group by filename and get metadata for each file
            var fileInfoList = new List<CsvFileInfoDto>();

            foreach (var job in csvJobs)
            {
                // Count records for this job
                var recordCount = await _unitOfWork.CsvRecords.CountAsync(x =>
                    x.JobId == job.Id && !x.IsDeleted
                );

                fileInfoList.Add(new CsvFileInfoDto
                {
                    FileName = job.FileName,
                    OriginalFileName = job.OriginalFileName,
                    JobId = job.Id,
                    UploadedAt = job.CreatedAt,
                    Status = job.Status.ToString(),
                    RecordCount = recordCount
                });
            }

            // 3. Build response
            var response = new ListCsvFilesResponseDto
            {
                TotalFiles = fileInfoList.Count,
                Files = fileInfoList.OrderByDescending(x => x.UploadedAt).ToList(),
                GeneratedAt = DateTime.UtcNow,
                Message = $"Tìm thấy {fileInfoList.Count} file CSV trong hệ thống."
            };

            return response;
        }

        public async Task<Stream> ExportSingleCsvFileAsync(string fileName)
        {
            // 1. Validate input
            if (string.IsNullOrWhiteSpace(fileName))
                throw ErrorHelper.BadRequest("Tên file không được để trống.");

            // 2. Check if file exists in database
            var csvJob = await _unitOfWork.CsvJobs.FirstOrDefaultAsync(x =>
                x.FileName == fileName && x.Type == CsvJobType.Import && !x.IsDeleted
            );

            if (csvJob == null)
                throw ErrorHelper.NotFound($"Không tìm thấy file '{fileName}' trong hệ thống.");

            // 3. Download file from MinIO
            try
            {
                var fileStream = await _blobService.DownloadFileAsync(fileName);
                return fileStream;
            }
            catch (Exception ex)
            {
                throw ErrorHelper.Internal($"Lỗi khi download file '{fileName}': {ex.Message}");
            }
        }

        public async Task<Stream> ExportAllCsvFilesAsync()
        {
            var csvJobs = await _unitOfWork.CsvJobs.GetAllAsync(x =>
                x.Type == CsvJobType.Import && !x.IsDeleted);

            if (csvJobs == null || csvJobs.Count == 0)
                throw ErrorHelper.BadRequest("Không có file CSV nào để export.");

            var uniqueFileNames = csvJobs
                .Select(x => x.FileName)
                .Distinct()
                .ToList();

            var zipStream = new MemoryStream();

            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var fileName in uniqueFileNames)
                {
                    await AddFileToZipArchiveAsync(archive, fileName);
                }
            }

            zipStream.Position = 0;
            return zipStream;
        }


        /// <summary>
        /// Parse multipart/form-data from request body and extract file information.
        /// Handles boundary extraction, content parsing, and file content extraction.
        /// </summary>
        public async Task<(string fileName, byte[] fileBytes)> ParseMultipartFormDataAsync(string contentType, Stream body)
        {
            if (string.IsNullOrEmpty(contentType) || !contentType.Contains("multipart/form-data"))
                throw ErrorHelper.BadRequest($"Invalid Content-Type: {contentType}. Expected multipart/form-data");

            // Read raw body
            using var memoryStream = new MemoryStream();
            await body.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            // Extract boundary from Content-Type
            var boundary = ExtractBoundary(contentType);

            // If no boundary in header, try to find from body
            if (string.IsNullOrEmpty(boundary))
            {
                boundary = ExtractBoundaryFromBody(memoryStream);
            }

            if (string.IsNullOrEmpty(boundary))
                throw ErrorHelper.BadRequest($"Missing boundary in multipart/form-data. Content-Type: {contentType}");

            // Parse multipart form data
            var (fileName, fileBytes) = await ParseMultipartFormData(memoryStream, boundary);

            if (string.IsNullOrEmpty(fileName) || fileBytes == null || fileBytes.Length == 0)
                throw ErrorHelper.BadRequest("No file found in request");

            return (fileName, fileBytes);
        }

        #region
        private async Task AddFileToZipArchiveAsync(ZipArchive archive, string fileName)
        {
            try
            {
                using var fileStream = await _blobService.DownloadFileAsync(fileName);
                var entry = archive.CreateEntry(fileName, CompressionLevel.Optimal);

                using var entryStream = entry.Open();
                await fileStream.CopyToAsync(entryStream);
            }
            catch (Exception ex)
            {
                throw ErrorHelper.Internal($"Lỗi khi download file '{fileName}': {ex.Message}");
            }
        }

        private string? ExtractBoundary(string contentTypeHeader)
        {
            try
            {
                var parts = contentTypeHeader.Split(';');
                foreach (var part in parts)
                {
                    var trimmed = part.Trim();
                    if (trimmed.StartsWith("boundary="))
                    {
                        var boundary = trimmed.Substring("boundary=".Length).Trim('"', ' ');
                        return boundary;
                    }
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }

        private string? ExtractBoundaryFromBody(MemoryStream stream)
        {
            try
            {
                stream.Position = 0;
                using var reader = new StreamReader(stream);
                var firstLine = reader.ReadLine();
                stream.Position = 0;

                // Boundary is usually the first line of multipart body, starting with --
                if (!string.IsNullOrEmpty(firstLine) && firstLine.StartsWith("--"))
                {
                    return firstLine.Substring(2); // Remove "--"
                }
            }
            catch (Exception)
            {
                return null;
            }
            return null;
        }

        private async Task<(string? fileName, byte[]? fileBytes)> ParseMultipartFormData(MemoryStream stream, string boundary)
        {
            try
            {
                stream.Position = 0;
                var reader = new StreamReader(stream);
                var content = await reader.ReadToEndAsync();

                // Find all parts
                var boundaryDelimiter = $"--{boundary}";
                var parts = content.Split(new[] { boundaryDelimiter }, StringSplitOptions.None);

                foreach (var part in parts)
                {
                    if (part.Contains("Content-Disposition") && part.Contains("filename="))
                    {
                        // Extract filename
                        var fileNameMatch = System.Text.RegularExpressions.Regex.Match(part, @"filename=""([^""]+)""");
                        if (!fileNameMatch.Success)
                            fileNameMatch = System.Text.RegularExpressions.Regex.Match(part, @"filename=([^\r\n;]+)");

                        string fileName = fileNameMatch.Success ? fileNameMatch.Groups[1].Value.Trim() : "uploaded_file.csv";

                        // Extract file content (after empty line)
                        var lines = part.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
                        int contentStartIndex = -1;

                        for (int i = 0; i < lines.Length; i++)
                        {
                            if (string.IsNullOrWhiteSpace(lines[i]))
                            {
                                contentStartIndex = i + 1;
                                break;
                            }
                        }

                        if (contentStartIndex > 0 && contentStartIndex < lines.Length)
                        {
                            var fileContent = string.Join("\n", lines.Skip(contentStartIndex));

                            // Remove trailing boundary markers and whitespace
                            fileContent = System.Text.RegularExpressions.Regex.Replace(fileContent, @"-+\s*$", "");
                            fileContent = fileContent.TrimEnd('\r', '\n');

                            var fileBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
                            return (fileName, fileBytes);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                throw ErrorHelper.Internal($"Error parsing multipart form data: {ex.Message}");
            }

            return (null, null);
        }
        /// <summary>
        /// Parse CSV file into CsvRecord list
        /// Skips multipart form-data headers if present
        /// Auto-detects if CSV has headers or is header-less
        /// </summary>
        private async Task<List<CsvRecord>> ParseCsvAsync(Guid jobId, string fileName, Stream fileStream)
        {
            var records = new List<CsvRecord>();
            
            fileStream.Position = 0;
            using var contentReader = new StreamReader(fileStream);

            string? line;
            var lines = new List<string>();
            
            // Read all lines and skip multipart headers
            while ((line = await contentReader.ReadLineAsync()) != null)
            {
                lines.Add(line);
            }

            // Find the actual CSV content start
            // Skip lines that are multipart headers (Content-Disposition, Content-Type, empty line)
            int csvStartIndex = 0;
            for (int i = 0; i < lines.Count; i++)
            {
                // Skip multipart headers
                if (lines[i].StartsWith("Content-Disposition:") || 
                    lines[i].StartsWith("Content-Type:") ||
                    string.IsNullOrWhiteSpace(lines[i]))
                {
                    continue;
                }
                
                // First non-header line is either the CSV header or first data line
                csvStartIndex = i;
                break;
            }

            // Ensure we have CSV content
            if (csvStartIndex >= lines.Count)
                return records;

            // Detect if first line is a header or data
            // Headers typically don't contain commas followed by numbers/dates/emails, or are descriptive
            var firstLine = lines[csvStartIndex];
            if (string.IsNullOrWhiteSpace(firstLine))
                return records;

            var firstLineValues = firstLine.Split(',').Select(v => v.Trim()).ToArray();
            
            // Simple heuristic: if values look like actual data (contain @ for emails, dates, etc), treat all as data
            bool isHeaderless = IsDataLine(firstLineValues);
            
            string[] headers;
            int dataStartIndex;

            if (isHeaderless)
            {
                // No header - generate column names as Column1, Column2, etc.
                headers = Enumerable.Range(1, firstLineValues.Length)
                    .Select(i => $"Column{i}")
                    .ToArray();
                dataStartIndex = csvStartIndex;
            }
            else
            {
                // First line is header
                headers = firstLineValues;
                dataStartIndex = csvStartIndex + 1;
            }

            // Parse CSV data rows
            for (int i = dataStartIndex; i < lines.Count; i++)
            {
                line = lines[i];
                
                // Skip empty lines and multipart boundary markers
                if (string.IsNullOrWhiteSpace(line) || line.StartsWith("--"))
                    continue;

                var values = line.Split(',').Select(v => v.Trim()).ToArray();

                // Create JSON object as string and parse it
                var jsonDict = new Dictionary<string, object>();
                for (int j = 0; j < headers.Length && j < values.Length; j++)
                {
                    jsonDict[headers[j]] = values[j];
                }

                var jsonString = JsonSerializer.Serialize(jsonDict);

                records.Add(new CsvRecord
                {
                    Id = Guid.NewGuid(),
                    JobId = jobId,
                    FileName = fileName,
                    ImportedAt = DateTime.UtcNow,
                    Data = jsonString
                });
            }

            return records;
        }

        /// <summary>
        /// Detect if a line is data (not headers)
        /// Heuristic: check if values contain typical data patterns like emails, dates, numbers, etc.
        /// </summary>
        private bool IsDataLine(string[] values)
        {
            if (values.Length == 0)
                return false;

            int dataIndicators = 0;

            foreach (var value in values)
            {
                // Check for email pattern
                if (value.Contains("@") && value.Contains("."))
                    dataIndicators++;
                
                // Check for date pattern (YYYY-MM-DD or similar)
                if (System.Text.RegularExpressions.Regex.IsMatch(value, @"\d{4}-\d{2}-\d{2}"))
                    dataIndicators++;
                
                // Check for GUID-like pattern (uppercase alphanumeric)
                if (System.Text.RegularExpressions.Regex.IsMatch(value, @"^[A-Z0-9]{8,}$"))
                    dataIndicators++;
                
                // Check for numeric value
                if (double.TryParse(value, out _))
                    dataIndicators++;
            }

            // If at least 30% of values look like data, it's a data line
            return dataIndicators >= (values.Length * 0.3);
        }
        #endregion

    }
}