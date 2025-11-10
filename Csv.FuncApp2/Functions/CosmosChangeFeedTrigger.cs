using CSVProssessor.Application.Interfaces;
using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Domain.DTOs.EmailDTOs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Csv.FuncApp2.Functions;

public class CosmosChangeFeedTrigger
{
    private readonly ICsvService _csvService;
    private readonly IEmailService _emailService;
    private readonly ILogger<CosmosChangeFeedTrigger> _logger;

    public CosmosChangeFeedTrigger(ILogger<CosmosChangeFeedTrigger> logger, ICsvService csvService, IEmailService emailService)
    {
        _logger = logger;
        _csvService = csvService;
        _emailService = emailService;
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

        // Publish message to topic
        await _csvService.PublishCsvChangeAsync("CosmosChangeDetected", new { RecordCount = input.Count, ChangedAt = DateTime.UtcNow });
        
        // Send email notification only once for the batch (not for each document)
        var emailRequest = new EmailRequestDto
        {
            To = "phuctg1@fpt.com"
        };
        await _emailService.SendDatabaseChanges(emailRequest);
        
        _logger.LogInformation("Database change notification email sent for batch of {count} records", input.Count);
    }
}

public class MyDocument
{
    public string? id { get; set; }

    public string? Text { get; set; }

    public int Number { get; set; }

    public bool Boolean { get; set; }
}