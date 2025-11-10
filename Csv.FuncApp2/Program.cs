using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

// Bỏ qua SSL check cho Cosmos SDK
builder.Services.AddSingleton(sp =>
{
    var cosmosOptions = new CosmosClientOptions
    {
        HttpClientFactory = () =>
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = (req, cert, chain, errors) => true
            };
            return new HttpClient(handler);
        }
    };

    string connectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString")
        ?? "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==";

    return new CosmosClient(connectionString, cosmosOptions);
});


builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

builder.Build().Run();
