using CSVProssessor.Application.Interfaces;
using CSVProssessor.Application.Utils;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;

namespace CSVProssessor.FunctionApp.Functions
{
    public class CsvFunctions
    {
        private readonly ICsvService _csvService;
        private readonly ILogger<CsvFunctions> _logger;

        public CsvFunctions(ICsvService csvService, ILogger<CsvFunctions> logger)
        {
            _csvService = csvService;
            _logger = logger;
        }

        [Function("UploadCsv")]
        [OpenApiOperation(operationId: "uploadCsv", tags: new[] { "CSV" })]
        [OpenApiRequestBody(contentType: "multipart/form-data", bodyType: typeof(FileUploadRequest))]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.Accepted, contentType: "application/json", bodyType: typeof(ApiResult<object>))]
        public async Task<HttpResponseData> UploadCsv(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "csv/upload")] HttpRequestData req)
        {
            try
            {
                var formData = await req.ReadFromJsonAsync<FileUploadRequest>();
                var file = formData.File;

                if (file == null)
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                var result = await _csvService.ImportCsvAsync(file);
                var response = req.CreateResponse(HttpStatusCode.Accepted);
                await response.WriteAsJsonAsync(ApiResult<object>.Success(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Upload error");
                var response = req.CreateResponse((HttpStatusCode)ExceptionUtils.ExtractStatusCode(ex));
                await response.WriteAsJsonAsync(ExceptionUtils.CreateErrorResponse<object>(ex));
                return response;
            }
        }

        [Function("ListCsvFiles")]
        [OpenApiOperation(operationId: "listCsvFiles", tags: new[] { "CSV" })]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ApiResult<object>))]
        public async Task<HttpResponseData> ListCsvFiles(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "csv/list")] HttpRequestData req)
        {
            try
            {
                var result = await _csvService.ListAllCsvFilesAsync();
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(ApiResult<object>.Success(result));
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "List error");
                var response = req.CreateResponse((HttpStatusCode)ExceptionUtils.ExtractStatusCode(ex));
                await response.WriteAsJsonAsync(ExceptionUtils.CreateErrorResponse<object>(ex));
                return response;
            }
        }
    }

    public class FileUploadRequest
    {
        public IFormFile File { get; set; }
    }
}