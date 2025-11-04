using Microsoft.Azure.Cosmos;
using CSVProssessor.Domain.Entities;
using Newtonsoft.Json;
using System.Net;

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
        // Skip SSL validation for Cosmos DB Emulator (self-signed certificate)
        var options = new CosmosClientOptions
        {
            HttpClientFactory = () =>
            {
                var handler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
                };
                return new HttpClient(handler);
            }
        };
        _cosmosClient = new CosmosClient(connectionString, options);
    }

    public async Task InitializeAsync(string databaseId)
    {
        Console.WriteLine($"[CosmosDbContext] === Starting Cosmos DB Initialization ===");
        Console.WriteLine($"[CosmosDbContext] Database ID: {databaseId}");
        Console.WriteLine($"[CosmosDbContext] CosmosClient initialized: {_cosmosClient != null}");
        
        const int maxRetries = 5;
        const int delayMs = 2000;
        
        try
        {
            // Create database if not exists - with retry logic
            Console.WriteLine("[CosmosDbContext] Attempting to create/get database...");
            Database? databaseResponse_db = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    Console.WriteLine($"[CosmosDbContext] Database creation attempt {attempt}/{maxRetries}...");
                    var databaseResponse = await _cosmosClient!.CreateDatabaseIfNotExistsAsync(databaseId);
                    databaseResponse_db = databaseResponse.Database;
                    Console.WriteLine($"[CosmosDbContext] ✓ Database '{databaseId}' created/ready");
                    Console.WriteLine($"[CosmosDbContext] Database ID: {databaseResponse_db.Id}");
                    Console.WriteLine($"[CosmosDbContext] Database Status: {databaseResponse.StatusCode}");
                    break;
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    Console.WriteLine($"[CosmosDbContext] ⚠ Attempt {attempt} failed: {ex.Message}");
                    if (attempt < maxRetries)
                    {
                        Console.WriteLine($"[CosmosDbContext] Retrying in {delayMs}ms...");
                        await Task.Delay(delayMs);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            
            _database = databaseResponse_db;

            // Create CsvJob container
            Console.WriteLine("[CosmosDbContext] Attempting to create 'CsvJobs' container...");
            var csvJobResponse = await _database!.CreateContainerIfNotExistsAsync(
                id: "CsvJobs",
                partitionKeyPath: "/Status",
                throughput: 400
            );
            _csvJobContainer = csvJobResponse.Container;
            Console.WriteLine($"[CosmosDbContext] ✓ Container 'CsvJobs' created/ready");
            Console.WriteLine($"[CosmosDbContext] CsvJobs Status: {csvJobResponse.StatusCode}");

            // Create CsvRecord container
            Console.WriteLine("[CosmosDbContext] Attempting to create 'CsvRecords' container...");
            var csvRecordResponse = await _database.CreateContainerIfNotExistsAsync(
                id: "CsvRecords",
                partitionKeyPath: "/JobId",
                throughput: 400
            );
            _csvRecordContainer = csvRecordResponse.Container;
            Console.WriteLine($"[CosmosDbContext] ✓ Container 'CsvRecords' created/ready");
            Console.WriteLine($"[CosmosDbContext] CsvRecords Status: {csvRecordResponse.StatusCode}");
            
            Console.WriteLine($"[CosmosDbContext] === Cosmos DB Initialization Completed Successfully ===");
        }
        catch (HttpRequestException ex)
        {
            Console.WriteLine($"[CosmosDbContext] ✗ HttpRequestException: {ex.Message}");
            Console.WriteLine($"[CosmosDbContext] Status Code: {ex.Data}");
            Console.WriteLine($"[CosmosDbContext] Inner Exception: {ex.InnerException?.Message}");
            throw;
        }
        catch (CosmosException ex)
        {
            Console.WriteLine($"[CosmosDbContext] ✗ CosmosException: {ex.Message}");
            Console.WriteLine($"[CosmosDbContext] Status Code: {ex.StatusCode}");
            Console.WriteLine($"[CosmosDbContext] SubStatus Code: {ex.SubStatusCode}");
            throw;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[CosmosDbContext] ✗ ERROR: {ex.GetType().Name}");
            Console.WriteLine($"[CosmosDbContext] Message: {ex.Message}");
            Console.WriteLine($"[CosmosDbContext] StackTrace: {ex.StackTrace}");
            throw;
        }
    }

    public void Dispose()
    {
        _cosmosClient?.Dispose();
    }
}
