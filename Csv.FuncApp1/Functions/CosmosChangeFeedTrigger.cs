using CSVProssessor.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Csv.FuncApp1.Functions;

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

        _logger.LogInformation($"[Function-app1]Documents modified: {input.Count}");

        foreach (var doc in input) await _csvService.PublishCsvChangeAsync("CosmosChangeDetected", doc);
    }
}

public class MyDocument
{
    public string id { get; set; }

    public string Text { get; set; }

    public int Number { get; set; }

    public bool Boolean { get; set; }
}