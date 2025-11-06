using CSVProssessor.Domain;
using CSVProssessor.FunctionApp.Architecture;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

var connectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException("CosmosDbConnectionString is not configured");
}

// Register CosmosDbContext as singleton
builder.Services.AddSingleton(sp =>
{
    var context = new CosmosDbContext(connectionString);
    var initTask = context.InitializeAsync("CSVProcessor");
    initTask.GetAwaiter().GetResult();
    return context;
});

builder.Services.SetupIocContainer();

builder.ConfigureFunctionsWebApplication();

var host = builder.Build();

// Force initialization of CosmosDbContext on startup
using (var scope = host.Services.CreateScope())
{
    var cosmosContext = scope.ServiceProvider.GetRequiredService<CosmosDbContext>();
}

host.Run();
