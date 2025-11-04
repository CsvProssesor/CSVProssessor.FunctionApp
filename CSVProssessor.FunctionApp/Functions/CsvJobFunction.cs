using CSVProssessor.Domain.Entities;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace CSVProssessor.FunctionApp.Functions;

public class CsvJobFunction
{
    private readonly IUnitOfWork _unitOfWork;

    public CsvJobFunction(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    [FunctionName("CreateCsvJob")]
    public async Task<IActionResult> CreateCsvJob(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "csv-jobs")] HttpRequest req,
        ILogger log)
    {
        try
        {
            log.LogInformation("Creating CSV Job");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var job = JsonConvert.DeserializeObject<CsvJob>(requestBody);

            if (job == null)
            {
                return new BadRequestObjectResult("Invalid request body");
            }

            // Create new job
            job.Id = Guid.NewGuid();
            await _unitOfWork.CsvJobs.AddAsync(job);
            await _unitOfWork.SaveChangesAsync();

            return new CreatedResult($"csv-jobs/{job.Id}", job);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error creating CSV Job");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [FunctionName("GetCsvJob")]
    public async Task<IActionResult> GetCsvJob(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "csv-jobs/{id}")] HttpRequest req,
        string id,
        ILogger log)
    {
        try
        {
            log.LogInformation($"Getting CSV Job: {id}");

            if (!Guid.TryParse(id, out var jobId))
            {
                return new BadRequestObjectResult("Invalid ID format");
            }

            var job = await _unitOfWork.CsvJobs.GetByIdAsync(jobId);

            if (job == null)
            {
                return new NotFoundResult();
            }

            return new OkObjectResult(job);
        }
        catch (Exception ex)
        {
            log.LogError(ex, $"Error getting CSV Job: {id}");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [FunctionName("GetAllCsvJobs")]
    public async Task<IActionResult> GetAllCsvJobs(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = "csv-jobs")] HttpRequest req,
        ILogger log)
    {
        try
        {
            log.LogInformation("Getting all CSV Jobs");

            var jobs = await _unitOfWork.CsvJobs.GetAllAsync();

            return new OkObjectResult(jobs);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Error getting all CSV Jobs");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [FunctionName("UpdateCsvJob")]
    public async Task<IActionResult> UpdateCsvJob(
        [HttpTrigger(AuthorizationLevel.Function, "put", Route = "csv-jobs/{id}")] HttpRequest req,
        string id,
        ILogger log)
    {
        try
        {
            log.LogInformation($"Updating CSV Job: {id}");

            if (!Guid.TryParse(id, out var jobId))
            {
                return new BadRequestObjectResult("Invalid ID format");
            }

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updatedJob = JsonConvert.DeserializeObject<CsvJob>(requestBody);

            if (updatedJob == null)
            {
                return new BadRequestObjectResult("Invalid request body");
            }

            updatedJob.Id = jobId;
            await _unitOfWork.CsvJobs.Update(updatedJob);
            await _unitOfWork.SaveChangesAsync();

            return new OkObjectResult(updatedJob);
        }
        catch (Exception ex)
        {
            log.LogError(ex, $"Error updating CSV Job: {id}");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }

    [FunctionName("DeleteCsvJob")]
    public async Task<IActionResult> DeleteCsvJob(
        [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "csv-jobs/{id}")] HttpRequest req,
        string id,
        ILogger log)
    {
        try
        {
            log.LogInformation($"Deleting CSV Job: {id}");

            if (!Guid.TryParse(id, out var jobId))
            {
                return new BadRequestObjectResult("Invalid ID format");
            }

            var job = await _unitOfWork.CsvJobs.GetByIdAsync(jobId);

            if (job == null)
            {
                return new NotFoundResult();
            }

            await _unitOfWork.CsvJobs.SoftRemove(job);
            await _unitOfWork.SaveChangesAsync();

            return new NoContentResult();
        }
        catch (Exception ex)
        {
            log.LogError(ex, $"Error deleting CSV Job: {id}");
            return new ObjectResult(new { error = ex.Message }) { StatusCode = 500 };
        }
    }
}
