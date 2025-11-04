using Microsoft.Azure.Cosmos;
using CSVProssessor.Domain.Entities;
using Newtonsoft.Json;

namespace CSVProssessor.Domain;

public class CosmosDbContext : IDisposable
{
    private readonly CosmosClient _cosmosClient;
    private Database? _database;
    private Container? _csvJobContainer;
    private Container? _csvRecordContainer;

    public Database Database => _database ?? throw new InvalidOperationException("Database not initialized. Call InitializeAsync first.");
    public Container CsvJobContainer => _csvJobContainer ?? throw new InvalidOperationException("CsvJobContainer not initialized. Call InitializeAsync first.");
    public Container CsvRecordContainer => _csvRecordContainer ?? throw new InvalidOperationException("CsvRecordContainer not initialized. Call InitializeAsync first.");

    public CosmosDbContext(string connectionString)
    {
        _cosmosClient = new CosmosClient(connectionString);
    }

    public async Task InitializeAsync(string databaseId)
    {
        // Create database if not exists
        var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(databaseId);
        _database = databaseResponse.Database;

        // Create CsvJob container
        _csvJobContainer = await _database.CreateContainerIfNotExistsAsync(
            id: "CsvJobs",
            partitionKeyPath: "/Status",
            throughput: 400
        );

        // Create CsvRecord container
        _csvRecordContainer = await _database.CreateContainerIfNotExistsAsync(
            id: "CsvRecords",
            partitionKeyPath: "/JobId",
            throughput: 400
        );
    }

    public void Dispose()
    {
        _cosmosClient?.Dispose();
    }
}
