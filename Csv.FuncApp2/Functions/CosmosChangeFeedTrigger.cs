using CSVProssessor.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Csv.FuncApp2.Functions;

public class CosmosChangeFeedTrigger
{
    private readonly ICsvService _csvService;
    private readonly ILogger<CosmosChangeFeedTrigger> _logger;

    public CosmosChangeFeedTrigger(ILogger<CosmosChangeFeedTrigger> logger, ICsvService csvService)
    {
        _logger = logger;
        _csvService = csvService;
    }

    [Function("CosmosChangeFeedTrigger")]
    public async Task RunAsync(
        [CosmosDBTrigger(
            "CSVProcessor",
            "CsvRecords",
            Connection = "CosmosDbConnectionString",
            LeaseContainerName = "leases",
            CreateLeaseContainerIfNotExists = true)]
        IReadOnlyList<MyDocument> input)
    {
        if (input == null || input.Count == 0)
            return;

        _logger.LogInformation("CSV records changed: {count}", input.Count);

        // Publish message to topic - DatabaseChangeNotificationService will subscribe and send email
        await _csvService.PublishCsvChangeAsync("CosmosChangeDetected", new { RecordCount = input.Count, ChangedAt = DateTime.UtcNow });
        
        _logger.LogInformation("Database change published to topic for batch of {count} records", input.Count);
    }
}

public class MyDocument
{
    public string? id { get; set; }

    public string? Text { get; set; }

    public int Number { get; set; }

    public bool Boolean { get; set; }
}