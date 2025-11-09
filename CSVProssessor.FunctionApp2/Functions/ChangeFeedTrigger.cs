using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using CSVProssessor.Domain.Entities;
using System.Text.Json;

namespace CSVProssessor.FunctionApp2.Functions;

public class ChangeFeedTrigger
{
    private readonly ILogger<ChangeFeedTrigger> _logger;

    public ChangeFeedTrigger(ILogger<ChangeFeedTrigger> logger)
    {
        _logger = logger;
    }

    [Function("CsvRecordsChangeFeedTrigger")]
    public void Run([CosmosDBTrigger(
        databaseName: "csvprocessor",
        containerName: "csvrecords",
        Connection = "CosmosDbConnectionString",
        LeaseContainerName = "leases",
        CreateLeaseContainerIfNotExists = true)] IReadOnlyList<CsvRecord> input)
    {
        if (input != null && input.Count > 0)
        {
            _logger.LogInformation("CsvRecords change feed triggered. Processing {count} changes.", input.Count);

            foreach (var csvRecord in input)
            {
                try
                {
                    _logger.LogInformation(
                        "CsvRecord changed - Id: {id}, JobId: {jobId}, FileName: {fileName}, ImportedAt: {importedAt}",
                        csvRecord.Id,
                        csvRecord.JobId,
                        csvRecord.FileName,
                        csvRecord.ImportedAt);

                    // Log the data content (truncated for readability)
                    var dataPreview = csvRecord.Data?.Length > 200 
                        ? csvRecord.Data[..200] + "..." 
                        : csvRecord.Data;
                    
                    _logger.LogInformation("CsvRecord data preview: {dataPreview}", dataPreview);

                    // Additional processing can be added here:
                    // - Send notifications
                    // - Update related entities
                    // - Trigger downstream processes
                    // - Update analytics/metrics
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, 
                        "Error processing CsvRecord change for Id: {id}, JobId: {jobId}", 
                        csvRecord.Id, 
                        csvRecord.JobId);
                }
            }

            _logger.LogInformation("Completed processing {count} CsvRecord changes.", input.Count);
        }
        else
        {
            _logger.LogInformation("CsvRecords change feed triggered but no changes detected.");
        }
    }
}