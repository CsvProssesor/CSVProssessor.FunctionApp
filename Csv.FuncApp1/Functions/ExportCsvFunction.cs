using System.Net;
using System.Web;
using CSVProssessor.Application.Interfaces;
using CSVProssessor.Application.Interfaces.Common;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;

namespace Csv.FuncApp1.Functions;

public class ExportCsvFunction
{
    private readonly IBlobService _blobService;
    private readonly ICsvService _csvService;
    private readonly ILogger<ExportCsvFunction> _logger;

    public ExportCsvFunction(
        ICsvService csvService,
        IBlobService blobService,
        ILogger<ExportCsvFunction> logger)
    {
        _csvService = csvService;
        _blobService = blobService;
        _logger = logger;
    }

    [Function("ExportAllCsv")]
    public async Task<HttpResponseData> ExportAllAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "csv/export/all")]
        HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Export all CSV files requested");

            // Tạo tên file zip duy nhất
            var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            var zipFileName = $"csv-export-{timestamp}.zip";

            // Zip tất cả file và upload lên folder export
            var sasUrl = await _blobService.ZipAndUploadAllAsync(zipFileName);

            _logger.LogInformation("All CSV files exported successfully");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                sasUrl,
                fileName = zipFileName,
                message = "CSV files exported successfully"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting all CSV files");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = ex.Message });
            return response;
        }
    }

    [Function("ExportSingleCsv")]
    public async Task<HttpResponseData> ExportSingleAsync(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "csv/export/single")]
        HttpRequestData req)
    {
        try
        {
            _logger.LogInformation("Export single CSV file requested");

            var query = HttpUtility.ParseQueryString(req.Url.Query);
            var fileName = query["fileName"];

            if (string.IsNullOrWhiteSpace(fileName))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteAsJsonAsync(new { error = "fileName query parameter is required" });
                return errorResponse;
            }

            var sasUrl = await _blobService.UploadSingleFileAsync(fileName);

            _logger.LogInformation("Single CSV file exported: {fileName}", fileName);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(new
            {
                sasUrl,
                fileName,
                message = "CSV file exported successfully"
            });

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting single CSV file");
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteAsJsonAsync(new { error = ex.Message });
            return response;
        }
    }
}