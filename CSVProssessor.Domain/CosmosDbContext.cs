using Microsoft.Azure.Cosmos;
using CSVProssessor.Domain.Entities;
using Newtonsoft.Json;
using System.Net;

namespace CSVProssessor.Domain;

public class CosmosDbContext : IDisposable, IAsyncDisposable
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
        // Use Gateway Mode for better compatibility with Emulator in Docker
        var options = new CosmosClientOptions
        {
            ConnectionMode = ConnectionMode.Gateway,
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
        const int maxRetries = 5;
        const int delayMs = 2000;
        
        try
        {
            Database? databaseResponse_db = null;
            
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var databaseResponse = await _cosmosClient!.CreateDatabaseIfNotExistsAsync(databaseId);
                    databaseResponse_db = databaseResponse.Database;
                    break;
                }
                catch (HttpRequestException) when (attempt < maxRetries)
                {
                    if (attempt < maxRetries)
                    {
                        await Task.Delay(delayMs);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
            
            _database = databaseResponse_db;

            // Create CsvJob container with retry
            // Note: If container already exists with different partition key, 
            // you need to delete the container first
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var csvJobResponse = await _database!.CreateContainerIfNotExistsAsync(
                        id: "CsvJobs",
                        partitionKeyPath: "/id"
                    );
                    _csvJobContainer = csvJobResponse.Container;
                    break;
                }
                catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.Conflict && attempt < maxRetries)
                {
                    // Container exists with different partition key - delete and recreate
                    try
                    {
                        await _database!.GetContainer("CsvJobs").DeleteContainerAsync();
                        await Task.Delay(delayMs);
                    }
                    catch { }
                }
                catch (CosmosException) when (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                }
            }

            // Create CsvRecord container with retry
            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    var csvRecordResponse = await _database!.CreateContainerIfNotExistsAsync(
                        id: "CsvRecords",
                        partitionKeyPath: "/JobId"
                    );
                    _csvRecordContainer = csvRecordResponse.Container;
                    break;
                }
                catch (CosmosException) when (attempt < maxRetries)
                {
                    await Task.Delay(delayMs);
                }
            }
        }
        catch (Exception)
        {
            throw;
        }
    }

    #region Disposal

    private bool _disposed;

    public void Dispose()
    {
        if (_disposed) return;

        try
        {
            _cosmosClient?.Dispose();
        }
        catch { }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;

        try
        {
            // CosmosClient in some SDK versions does not implement DisposeAsync.
            // Call the synchronous Dispose to ensure resources are released.
            _cosmosClient?.Dispose();
        }
        catch { }

        _disposed = true;
        GC.SuppressFinalize(this);
    }

    #endregion
}
