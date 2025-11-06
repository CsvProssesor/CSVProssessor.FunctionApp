using CSVProssessor.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CSVProssessor.FunctionApp.Functions;

public class CsvJobFunction
{
    private readonly ICsvService _csvService;
    private readonly ILogger<CsvJobFunction> _logger;

    public CsvJobFunction(ICsvService csvService, ILogger<CsvJobFunction> logger)
    {
        _csvService = csvService;
        _logger = logger;
    }

    [Function("ImportCsvJob")]
    public async Task<HttpResponseData> ImportCsvJob(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "csv/import")] HttpRequestData req)
    {
        _logger.LogInformation("ImportCsvJob function called.");

        try
        {
            // Log all headers for debugging
            foreach (var header in req.Headers)
            {
                _logger.LogInformation($"Header: {header.Key} = {string.Join(", ", header.Value)}");
            }

            // Đọc raw body
            using var memoryStream = new MemoryStream();
            await req.Body.CopyToAsync(memoryStream);
            memoryStream.Position = 0;

            _logger.LogInformation($"Request body size: {memoryStream.Length} bytes");

            // Kiểm tra content type
            if (!req.Headers.Contains("Content-Type"))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Content-Type header is missing");
                return errorResponse;
            }

            var contentType = req.Headers.GetValues("Content-Type").First();
            _logger.LogInformation($"Content-Type: {contentType}");

            if (!contentType.Contains("multipart/form-data"))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Invalid Content-Type: {contentType}. Expected multipart/form-data");
                return errorResponse;
            }

            // Extract boundary từ Content-Type
            var boundary = ExtractBoundary(contentType);
            _logger.LogInformation($"Boundary from header: '{boundary}'");

            // Nếu không có boundary trong header, thử tìm từ body
            if (string.IsNullOrEmpty(boundary))
            {
                boundary = ExtractBoundaryFromBody(memoryStream);
                _logger.LogInformation($"Boundary from body: '{boundary}'");
            }

            if (string.IsNullOrEmpty(boundary))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync($"Missing boundary in multipart/form-data. Content-Type: {contentType}");
                return errorResponse;
            }

            // Parse multipart form data
            var (fileName, fileBytes) = await ParseMultipartFormData(memoryStream, boundary);

            if (string.IsNullOrEmpty(fileName) || fileBytes == null || fileBytes.Length == 0)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("No file found in request");
                return errorResponse;
            }

            _logger.LogInformation($"File received: {fileName}, Size: {fileBytes.Length} bytes");

            // Tạo FormFile object từ bytes
            using var fileStream = new MemoryStream(fileBytes);
            var formFile = new FormFile(fileStream, 0, fileBytes.Length, "file", fileName);

            // Gọi hàm ImportCsvAsync từ CsvService
            var result = await _csvService.ImportCsvAsync(formFile);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error in ImportCsvJob: {ex.Message}\n{ex.StackTrace}");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }

    private string ExtractBoundary(string contentTypeHeader)
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
        catch (Exception ex)
        {
            _logger.LogError($"Error extracting boundary: {ex.Message}");
        }
        return null;
    }

    private string ExtractBoundaryFromBody(MemoryStream stream)
    {
        try
        {
            stream.Position = 0;
            using var reader = new StreamReader(stream);
            var firstLine = reader.ReadLine();
            stream.Position = 0;

            // Boundary thường là dòng đầu tiên của multipart body, bắt đầu với --
            if (!string.IsNullOrEmpty(firstLine) && firstLine.StartsWith("--"))
            {
                return firstLine.Substring(2); // Bỏ đi "--"
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error extracting boundary from body: {ex.Message}");
        }
        return null;
    }

    private async Task<(string fileName, byte[] fileBytes)> ParseMultipartFormData(MemoryStream stream, string boundary)
    {
        try
        {
            stream.Position = 0;
            var reader = new StreamReader(stream);
            var content = await reader.ReadToEndAsync();

            // Tìm tất cả parts
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
                        fileContent = System.Text.RegularExpressions.Regex.Replace(fileContent, @"--\s*$", "");
                        fileContent = fileContent.TrimEnd('\r', '\n', '-');

                        var fileBytes = System.Text.Encoding.UTF8.GetBytes(fileContent);
                        return (fileName, fileBytes);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Error parsing multipart form data: {ex.Message}\n{ex.StackTrace}");
        }

        return (null, null);
    }
}