using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace CSVProssessor.FunctionApp1.Functions
{
    public class HelloWorldFunction
    {
        private readonly ILogger<HelloWorldFunction> _logger;

        public HelloWorldFunction(ILogger<HelloWorldFunction> logger)
        {
            _logger = logger;
        }

        [Function("HelloWorld")]
        public IActionResult Run([HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "hello")] HttpRequest req)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");
            return new OkObjectResult("hello world hehehehehehe");
        }

        [Function("UploadFile")]
        public async Task<IActionResult> UploadFile(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "upload")] HttpRequest req)
        {
            _logger.LogInformation("Processing file upload");

            try
            {
                // Kiểm tra Content-Type
                if (!req.ContentType?.StartsWith("multipart/form-data") ?? true)
                {
                    return new BadRequestObjectResult(new { error = "Content-Type must be multipart/form-data" });
                }

                var formData = await req.ReadFormAsync(new FormOptions());
                var file = formData.Files.FirstOrDefault();

                if (file == null || file.Length == 0)
                {
                    return new BadRequestObjectResult(new { error = "No file provided" });
                }

                // Đọc file content
                using (var stream = file.OpenReadStream())
                using (var streamReader = new StreamReader(stream))
                {
                    var content = await streamReader.ReadToEndAsync();
                    var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

                    return new OkObjectResult(new
                    {
                        success = true,
                        message = "File uploaded successfully",
                        fileName = file.FileName,
                        fileSize = file.Length,
                        lineCount = lines.Length - 1 // Trừ 1 vì dòng cuối có thể trống
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error uploading file: {ex.Message}");
                return new ObjectResult(new { error = ex.Message })
                {
                    StatusCode = StatusCodes.Status500InternalServerError
                };
            }
        }
    }

    // DTO for OpenAPI
    public class FileParameter
    {
        public IFormFile? file { get; set; }
    }
}