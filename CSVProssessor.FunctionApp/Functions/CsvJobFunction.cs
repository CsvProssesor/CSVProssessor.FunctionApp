using CSVProssessor.Application.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Net;

namespace CSVProssessor.FunctionApp1.Functions;

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
        try
        {
            if (!req.Headers.Contains("Content-Type"))
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await errorResponse.WriteStringAsync("Content-Type header is missing");
                return errorResponse;
            }
            var contentType = req.Headers.GetValues("Content-Type").First();

            var (fileName, fileBytes) = await _csvService.ParseMultipartFormDataAsync(contentType, req.Body);

            using var fileStream = new MemoryStream(fileBytes);
            var formFile = new FormFile(fileStream, 0, fileBytes.Length, "file", fileName);

            var result = await _csvService.ImportCsvAsync(formFile);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
        catch (Exception ex)
        {
            var response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error: {ex.Message}");
            return response;
        }
    }
}