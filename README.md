# CSV Processor - Azure Functions & Serverless Architecture

A serverless CSV processing system built on **Azure Functions**, featuring event-driven architecture with **Azure Cosmos DB**, **Azure Service Bus**, and real-time change detection using **Cosmos DB Change Feed Trigger**.

## Key Features

### CSV Import (Asynchronous via Service Bus)
- Upload CSV file → Trigger message to **Azure Service Bus Queue** (`csv-import-queue`)
- Azure Function processes queue messages asynchronously
- Parse CSV → Store records in **Azure Cosmos DB**
- Automatic duplicate prevention with unique identifiers

### CSV Export
- **Export Single File**: Download specific CSV by filename
- **Export All Files**: Download all CSVs as ZIP archive
- **List All Files**: View metadata (filename, status, record count, upload time)
- Direct file streaming with Azure Blob Storage

### Background Processing

1. **Service Bus Triggered Function** (`CsvImportQueueTrigger`)
   - Listens to Azure Service Bus Queue (`csv-import-queue`)
   - Receives import messages → Processes CSV data
   - Stores processed records in Azure Cosmos DB
   - Auto-retry on failure with dead-letter queue (DLQ) support

2. **Cosmos DB Change Feed Trigger** (`CsvChangeDetectionFunction`)
   - Monitors **Cosmos DB Change Feed** in real-time
   - Detects new/updated CSV records automatically
   - Publishes notifications to **Azure Service Bus Topic** (`csv-changes-topic`)
   - Logs changes and triggers downstream processing
   - Maintains lease collection for distributed processing

## Tech Stack

| Component | Technology | Details |
|-----------|-----------|---------|
| Compute | Azure Functions | Serverless, event-driven |
| Database | Azure Cosmos DB | NoSQL, global distribution |
| Message Broker | Azure Service Bus | Queues & Topics for messaging |
| Blob Storage | Azure Blob Storage | CSV file storage & retrieval |
| Triggers | Cosmos DB Change Feed | Real-time change detection |
| Runtime | .NET 8.0 | Isolated worker model |
| IaC | Azure Bicep | Infrastructure as Code |
| Monitoring | Application Insights | Logging & telemetry |

## Project Structure (Clean Architecture Pattern)

```
CSVProssessor.Api-main/
├── CSVProssessor.FunctionApp/              # Azure Functions Entry Point
│   ├── Functions/                          # Function Triggers
│   │   ├── CsvImportQueueTrigger.cs       # Service Bus Queue Trigger
│   │   ├── CsvChangeDetectionTrigger.cs   # Cosmos DB Change Feed Trigger
│   │   ├── CsvUploadHttpTrigger.cs        # HTTP Trigger for upload
│   │   ├── CsvExportHttpTrigger.cs        # HTTP Trigger for export
│   │   └── CsvListHttpTrigger.cs          # HTTP Trigger for listing
│   │
│   ├── Architecture/                       # Configuration & Setup
│   │   └── IocContainer.cs                # Dependency Injection
│   │
│   ├── Program.cs                          # Function App entry point
│   ├── host.json                           # Function runtime config
│   ├── local.settings.json                 # Local development config
│   ├── CSVProssessor.FunctionApp.csproj    # Project file
│   └── .gitignore
│
├── CSVProssessor.Application/              # Business Logic Layer
│   ├── Interfaces/                         # Service Contracts
│   │   ├── ICsvService.cs                 # CSV processing contract
│   │   ├── IBlobService.cs                # Blob Storage contract
│   │   ├── IServiceBusService.cs          # Service Bus contract
│   │   └── Common/
│   │       ├── IClaimsService.cs
│   │       ├── ICurrentTime.cs
│   │       └── ILoggerService.cs
│   │
│   ├── Services/                           # Service Implementations
│   │   ├── CsvService.cs                  # CSV parsing & validation
│   │   ├── Common/
│   │   │   ├── ClaimsService.cs
│   │   │   ├── CurrentTime.cs
│   │   │   └── LoggerService.cs
│   │   └── (Additional business services)
│   │
│   ├── Worker/                             # Background Jobs
│   │   ├── CsvImportQueueListenerService.cs
│   │   └── ChangeDetectionBackgroundService.cs
│   │
│   ├── Utils/                              # Utilities & Helpers
│   │   ├── Constants.cs
│   │   ├── Exceptions.cs
│   │   └── Extensions.cs
│   │
│   └── CSVProssessor.Application.csproj
│
├── CSVProssessor.Domain/                   # Domain Layer
│   ├── Entities/                           # Domain Entities
│   │   ├── CsvRecord.cs
│   │   └── (Other entities)
│   │
│   ├── DTOs/                               # Data Transfer Objects
│   │   ├── CsvRecordDto.cs
│   │   ├── ImportRequest.cs
│   │   ├── ChangeNotification.cs
│   │   └── ApiResponse.cs
│   │
│   ├── Enums/                              # Enumerations
│   │   ├── CsvStatus.cs
│   │   └── (Other enums)
│   │
│   ├── Migrations/                         # Database Migrations
│   │
│   ├── AppDbContext.cs                    # Cosmos DB Context (if using EF Core)
│   └── CSVProssessor.Domain.csproj
│
├── CSVProssessor.Infrastructure/           # Infrastructure Layer
│   ├── Interfaces/                         # Repository Contracts
│   │   └── (Repository interfaces)
│   │
│   ├── Repositories/                       # Repository Implementations
│   │   ├── GenericRepository.cs           # Generic CRUD operations
│   │   └── (Specific repositories)
│   │
│   ├── Commons/                            # Infrastructure Services
│   │   ├── BlobService.cs                 # Blob Storage operations
│   │   ├── ServiceBusService.cs           # Service Bus operations
│   │   └── (Other infrastructure services)
│   │
│   ├── Utils/                              # Infrastructure Utilities
│   │
│   ├── UnitOfWork.cs                      # Unit of Work Pattern
│   └── CSVProssessor.Infrastructure.csproj
│
├── CSVProssessor.sln                       # Solution file
├── docker-compose.yml                      # Docker Compose config
├── docker-compose.override.yml
│
├── infra/                                  # Infrastructure as Code
│   ├── main.bicep                         # Main Bicep template
│   ├── modules/
│   │   ├── cosmosdb.bicep                 # Cosmos DB provisioning
│   │   ├── servicebus.bicep               # Service Bus provisioning
│   │   ├── storage.bicep                  # Storage Account provisioning
│   │   └── functionapp.bicep              # Function App provisioning
│   └── parameters.json
│
└── README.md
```

## Layer Responsibilities

### CSVProssessor.FunctionApp (Presentation Layer)
- Azure Function triggers (HTTP, Service Bus, Cosmos DB Change Feed)
- Request/response handling
- Dependency injection configuration
- Calls Application layer services

### CSVProssessor.Application (Business Logic Layer)
- Core business logic for CSV processing
- Service implementations (ICsvService, IBlobService, IServiceBusService)
- Background workers (queue listeners, change detection)
- No direct dependency on Azure services

### CSVProssessor.Domain (Core/Entity Layer)
- Domain entities (CsvRecord, etc.)
- Data Transfer Objects (DTOs)
- Enumerations
- Database context (if using EF Core)
- No external dependencies

### CSVProssessor.Infrastructure (Data Access Layer)
- Repository pattern implementations
- Azure service integrations (Blob Storage, Service Bus, Cosmos DB)
- Unit of Work pattern
- Database migrations
- External API calls

## Architecture Layers Overview

### Dependency Flow
```
FunctionApp (Entry Point)
    ↓ (HTTP/Queue/Change Feed Triggers)
Application Layer (Business Logic)
    ↓ (Implements interfaces)
Domain Layer (Entities, DTOs)
    ↑ (References)
Infrastructure Layer (Data Access, Azure Services)
    ↓ (Depends on)
External Services (Cosmos DB, Service Bus, Blob Storage)
```

### Setup & Configuration (Architecture/IocContainer.cs)
```csharp
public static IServiceCollection SetupIocContainer(this IServiceCollection services)
{
    // 1. Database Context Setup
    services.SetupDbContext();
    
    // 2. Business Services Registration
    services.SetupBusinessServicesLayer();
    
    // 3. Infrastructure Setup
    services.AddScoped<IUnitOfWork, UnitOfWork>();
    services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
    
    // 4. Azure Services Registration
    services.AddScoped<IBlobService, BlobService>();
    services.AddScoped<IServiceBusService, ServiceBusService>();
    services.AddScoped<ICsvService, CsvService>();
    
    // 5. Background Services
    services.AddHostedService<CsvImportQueueListenerService>();
    services.AddHostedService<ChangeDetectionBackgroundService>();
    
    // 6. Common Services
    services.AddScoped<IClaimsService, ClaimsService>();
    services.AddScoped<ICurrentTime, CurrentTime>();
    services.AddScoped<ILoggerService, LoggerService>();
    
    return services;
}
```

## Prerequisites

- **Azure Account** with active subscription
- **Azure CLI** v2.50+
- **.NET 8.0 SDK**
- **Azure Functions Core Tools** v4.0+
- **Visual Studio Code** or **Visual Studio 2022**
- **PowerShell** 5.1+ (Windows) or **Bash** (Linux/Mac)

## Quick Start

### 1. Clone Repository
```bash
git clone <repository-url>
cd CSVProssessor.FunctionApp
```

### 2. Create Azure Resources
```bash
# Login to Azure
az login

# Create Resource Group
az group create --name rg-csv-processor --location eastus

# Deploy infrastructure
az deployment group create `
  --resource-group rg-csv-processor `
  --template-file infra/main.bicep `
  --parameters environment=prod
```

### 3. Deploy Function App
```bash
# Deploy to Azure
func azure functionapp publish <FunctionAppName>
```

### 4. Configure Local Development
```bash
# Copy example settings
cp local.settings.example.json local.settings.json

# Update connection strings for:
# - COSMOS_CONNECTION_STRING
# - SERVICE_BUS_CONNECTION_STRING
# - STORAGE_CONNECTION_STRING
```

### 5. Run Locally
```bash
# Start function runtime
func start

# In another terminal, test endpoints
curl http://localhost:7071/api/csv-upload
```

## Azure Function Triggers & Bindings

### 1. Service Bus Queue Trigger - CSV Import
```
Trigger: Azure Service Bus Queue (csv-import-queue)
Input Binding: Service Bus message
Output Binding: Cosmos DB (insert records)
Retry Policy: Exponential backoff (max 3 retries)
Dead Letter Queue: csv-import-queue-dlq
```

**Function Implementation:**
```csharp
[Function("CsvImportQueueTrigger")]
public async Task Run(
    [ServiceBusTrigger("csv-import-queue", Connection = "SERVICE_BUS_CONNECTION_STRING")] 
    ServiceBusReceivedMessage message,
    IAsyncCollector<dynamic> cosmosOutput,
    ILogger log)
{
    try
    {
        var importRequest = JsonSerializer.Deserialize<ImportRequest>(message.Body.ToString());
        var csvData = await _blobService.DownloadAsync(importRequest.FileName);
        var records = await _csvService.ParseCsvAsync(csvData);
        
        foreach (var record in records)
        {
            await cosmosOutput.AddAsync(record);
        }
        
        await message.CompleteAsync();
        log.LogInformation($"CSV {importRequest.FileName} processed successfully");
    }
    catch (Exception ex)
    {
        log.LogError($"Error processing CSV: {ex.Message}");
        await message.DeadLetterAsync();
    }
}
```

**Function Flow:**
```
Service Bus Message 
  → Deserialize CSV data
  → Validate records
  → Insert into Cosmos DB
  → Publish success event
```

### 2. Cosmos DB Change Feed Trigger - Change Detection
```
Trigger: Cosmos DB Change Feed (leases collection)
Container: csv-records
Lease Collection: csv-records-leases
Max Items Per Invocation: 100
Start From Beginning: false
Output Binding: Service Bus Topic (csv-changes-topic)
```

**Function Implementation:**
```csharp
[Function("CsvChangeDetectionTrigger")]
public async Task Run(
    [CosmosDBTrigger(
        databaseName: "CsvProcessor",
        collectionName: "csv-records",
        ConnectionStringSetting = "COSMOS_CONNECTION_STRING",
        LeaseCollectionName = "csv-records-leases",
        LeaseConnectionStringSetting = "COSMOS_CONNECTION_STRING",
        LeaseDatabase = "CsvProcessor",
        MaxItemsPerInvocation = 100)]
    IReadOnlyList<dynamic> changes,
    [ServiceBus("csv-changes-topic", Connection = "SERVICE_BUS_CONNECTION_STRING")]
    IAsyncCollector<ChangeNotification> serviceBusMessages,
    ILogger log)
{
    if (changes.Count > 0)
    {
        foreach (var change in changes)
        {
            var notification = new ChangeNotification
            {
                RecordId = change.id,
                Timestamp = DateTime.UtcNow,
                ChangeType = "Updated"
            };
            
            await serviceBusMessages.AddAsync(notification);
            log.LogInformation($"Change detected: {change.id}");
        }
    }
}
```

**Function Flow:**
```
Cosmos DB Change Feed
  → Detect new/updated records
  → Extract change metadata
  → Publish to Service Bus Topic
  → Update lease checkpoint
```

### 3. HTTP Trigger - CSV Operations
```
Trigger: HTTP POST/GET
Routes:
  POST   /api/csv-upload       → Enqueue message to Service Bus
  GET    /api/csv-list         → Query Cosmos DB
  GET    /api/csv-export/{id}  → Download from Blob Storage
  GET    /api/csv-export-all   → Create & return ZIP archive
```

**Function Implementation:**
```csharp
[Function("CsvUploadHttpTrigger")]
public async Task<HttpResponseData> UploadCsv(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "csv-upload")] 
    HttpRequestData req,
    [ServiceBus("csv-import-queue", Connection = "SERVICE_BUS_CONNECTION_STRING")]
    IAsyncCollector<ImportRequest> queueMessages,
    ILogger log)
{
    try
    {
        // Handle file upload and save to Blob Storage
        var fileName = $"{Guid.NewGuid()}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv";
        var fileContent = await req.Body.ReadAsAsync<byte[]>();
        
        await _blobService.UploadAsync(fileName, fileContent);
        
        // Queue message for processing
        var importRequest = new ImportRequest { FileName = fileName };
        await queueMessages.AddAsync(importRequest);
        
        var response = req.CreateResponse(HttpStatusCode.Accepted);
        await response.WriteAsJsonAsync(new { jobId = fileName });
        return response;
    }
    catch (Exception ex)
    {
        log.LogError($"Upload error: {ex.Message}");
        var response = req.CreateResponse(HttpStatusCode.InternalServerError);
        await response.WriteAsJsonAsync(new { error = ex.Message });
        return response;
    }
}
```

## Configuration

### Azure Resources Required

**Azure Cosmos DB:**
- Account: `csv-processor-db`
- Database: `CsvProcessor`
- Collections:
  - `csv-records` (with Change Feed enabled)
  - `csv-records-leases` (for Change Feed processor)
- Partition Key: `/fileName`
- RU/s: 400 (auto-scale 400-4000)

**Azure Service Bus:**
- Namespace: `csv-processor-sb`
- Queue: `csv-import-queue` (with DLQ)
- Topic: `csv-changes-topic`
- Subscription: `csv-changes-subscription`

**Azure Storage Account:**
- Account: `csvprocessorstorage`
- Container: `csv-files`
- Access Level: Private

**Azure Function App:**
- Name: `csv-processor-func`
- Runtime: .NET 8.0
- Plan: Consumption (Pay-as-you-go)
- Application Insights: Enabled

### Connection Strings (local.settings.json)
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=https;...",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "COSMOS_CONNECTION_STRING": "AccountEndpoint=https://csv-processor-db.documents.azure.com:443/;...",
    "SERVICE_BUS_CONNECTION_STRING": "Endpoint=sb://csv-processor-sb.servicebus.windows.net/;...",
    "STORAGE_CONNECTION_STRING": "DefaultEndpointsProtocol=https;...",
    "COSMOS_DATABASE_ID": "CsvProcessor",
    "COSMOS_CONTAINER_ID": "csv-records"
  }
}
```

## Monitoring & Logging

### Application Insights Metrics
- Function execution count & duration
- Service Bus queue depth & processing rate
- Cosmos DB throughput & latency
- Change Feed lag & catch-up time
- Error rates & exceptions

### Query Examples (Kusto Query Language)
```kusto
// Function execution performance
requests
| where name contains "CsvImportQueueTrigger"
| summarize avg(duration), max(duration) by name

// Service Bus message processing
customEvents
| where name == "ServiceBusMessageProcessed"
| summarize count() by tostring(customDimensions.status)

// Cosmos DB change detection latency
customMetrics
| where name == "ChangeDetectionLatency"
| summarize avg(value) by bin(timestamp, 1m)
```

## Security Best Practices

1. **Connection Strings**: Store in Azure Key Vault
   ```bash
   az keyvault secret set --vault-name csv-processor-kv \
     --name CosmosConnectionString \
     --value "<connection-string>"
   ```

2. **Managed Identities**: Use system-assigned identities
   ```
   Function App → Cosmos DB (RBAC)
   Function App → Service Bus (RBAC)
   Function App → Storage (RBAC)
   ```

3. **Network Security**: Configure Private Endpoints & VNet integration
   - Cosmos DB Private Endpoint
   - Service Bus Private Endpoint
   - Storage Private Endpoint

4. **Change Feed Security**:
   - Lease collection in separate database
   - Read-only access for change detection
   - Monitor unauthorized access via audit logs

## Deployment Pipeline (CI/CD)

### GitHub Actions Workflow
```yaml
name: Deploy Function App
on:
  push:
    branches: [main]
jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0'
      - run: dotnet build
      - run: dotnet publish -c Release
      - uses: Azure/functions-action@v1
        with:
          app-name: 'csv-processor-func'
          package: 'publish'
          publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
```

## Troubleshooting

### Change Feed Not Detecting Changes
- Verify Change Feed enabled on Cosmos DB container
- Check lease collection exists and is writable
- Ensure function app has Contributor role on Cosmos DB
- Check Application Insights for Change Feed lag metrics

### Service Bus Messages Not Processing
- Verify Service Bus connection string in settings
- Check queue/topic exists and is accessible
- Review Message Pump configuration
- Check function app identity has Service Bus Data Receiver role

### Performance Optimization
- Increase Cosmos DB RU/s during peak loads
- Tune Change Feed batch size (MaxItemsPerInvocation)
- Enable Cosmos DB indexing on frequently queried fields
- Use Service Bus partitioned queues for throughput

## Additional Resources

- [Azure Functions Documentation](https://learn.microsoft.com/en-us/azure/azure-functions/)
- [Cosmos DB Change Feed](https://learn.microsoft.com/en-us/azure/cosmos-db/change-feed)
- [Azure Service Bus Messaging](https://learn.microsoft.com/en-us/azure/service-bus-messaging/)
- [Azure Functions Bindings](https://learn.microsoft.com/en-us/azure/azure-functions/functions-bindings-storage-blob)
- [Best Practices for Cosmos DB](https://learn.microsoft.com/en-us/azure/cosmos-db/best-practices)

## License

This project is licensed under the MIT License - see the LICENSE file for details.
