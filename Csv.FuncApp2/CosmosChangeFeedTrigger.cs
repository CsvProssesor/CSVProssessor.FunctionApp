using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Csv.FuncApp2;

public class CosmosChangeFeedTrigger
{
    private readonly ILogger<CosmosChangeFeedTrigger> _logger;

    public CosmosChangeFeedTrigger(ILogger<CosmosChangeFeedTrigger> logger)
    {
        _logger = logger;
    }

    [Function("CosmosChangeFeedTrigger")]
    public void Run([CosmosDBTrigger(
        databaseName: "CSVProcessor",
        containerName: "CsvRecords",
        Connection = "CosmosDbConnectionString",
        LeaseContainerName = "leases",
        CreateLeaseContainerIfNotExists = true)] IReadOnlyList<MyDocument> input)
    {
        if (input != null && input.Count > 0)
        {
            _logger.LogInformation("Documents modified: " + input.Count);
            _logger.LogInformation("First document Id: " + input[0].id);
        }
    }
}

public class MyDocument
{
    public string id { get; set; }

    public string Text { get; set; }

    public int Number { get; set; }

    public bool Boolean { get; set; }
}