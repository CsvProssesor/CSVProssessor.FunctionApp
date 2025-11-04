using CSVProssessor.Domain;
using CSVProssessor.Infrastructure;
using CSVProssessor.Infrastructure.Commons;
using CSVProssessor.Infrastructure.Interfaces;
using CSVProssessor.Infrastructure.Repositories;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

Console.WriteLine("[Program] === Azure Functions Startup ===");

var connectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString");
Console.WriteLine($"[Program] Reading CosmosDbConnectionString from environment...");
Console.WriteLine($"[Program] Connection string found: {!string.IsNullOrEmpty(connectionString)}");
if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("[Program] ERROR: CosmosDbConnectionString is not configured!");
    throw new InvalidOperationException("CosmosDbConnectionString is not configured");
}

Console.WriteLine($"[Program] Connection string (masked): {connectionString.Substring(0, Math.Min(50, connectionString.Length))}...");
Console.WriteLine($"[Program] Full connection string: {connectionString}");
Console.WriteLine($"[Program] Connection string length: {connectionString.Length}");

// Register CosmosDbContext as singleton
builder.Services.AddSingleton(sp =>
{
    Console.WriteLine("[Program.DI] Creating CosmosDbContext instance...");
    try
    {
        var context = new CosmosDbContext(connectionString);
        Console.WriteLine("[Program.DI] CosmosDbContext created, starting initialization...");
        
        var initTask = context.InitializeAsync("CSVProcessor");
        initTask.GetAwaiter().GetResult();
        
        Console.WriteLine("[Program.DI] ✓ Cosmos DB initialization completed successfully!");
        return context;
    }
    catch (Exception ex)
    {
        Console.WriteLine("[Program.DI] ✗ ERROR during Cosmos DB initialization");
        Console.WriteLine($"[Program.DI] Exception Type: {ex.GetType().FullName}");
        Console.WriteLine($"[Program.DI] Message: {ex.Message}");
        Console.WriteLine($"[Program.DI] StackTrace: {ex.StackTrace}");
        if (ex.InnerException != null)
        {
            Console.WriteLine($"[Program.DI] Inner Exception: {ex.InnerException.Message}");
        }
        throw;
    }
});

Console.WriteLine("[Program] Registering application services...");
// Register common services
builder.Services.AddScoped<ICurrentTime, CurrentTime>();
builder.Services.AddScoped<IClaimsService, ClaimsService>();
builder.Services.AddScoped<ILoggerService, LoggerService>();

// Register UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(CosmosRepository<>));
Console.WriteLine("[Program] ✓ All services registered");

builder.ConfigureFunctionsWebApplication();

Console.WriteLine("[Program] Building host...");
var host = builder.Build();
Console.WriteLine("[Program] ✓ Host built successfully");

// Force initialization of CosmosDbContext on startup
Console.WriteLine("[Program] Forcing CosmosDbContext initialization...");
try
{
    using (var scope = host.Services.CreateScope())
    {
        var cosmosContext = scope.ServiceProvider.GetRequiredService<CosmosDbContext>();
        Console.WriteLine("[Program] ✓ CosmosDbContext initialized during startup");
    }
}
catch (Exception ex)
{
    Console.WriteLine($"[Program] ✗ Failed to initialize CosmosDbContext: {ex.Message}");
    Console.WriteLine($"[Program] StackTrace: {ex.StackTrace}");
    throw;
}

Console.WriteLine("[Program] Starting host...");
host.Run();
Console.WriteLine("[Program] === Azure Functions Stopped ===");
