using CSVProssessor.Domain;
using CSVProssessor.Infrastructure;
using CSVProssessor.Infrastructure.Commons;
using CSVProssessor.Infrastructure.Interfaces;
using CSVProssessor.Infrastructure.Repositories;
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

// Register common services
builder.Services.AddScoped<ICurrentTime, CurrentTime>();
builder.Services.AddScoped<IClaimsService, ClaimsService>();
builder.Services.AddScoped<ILoggerService, LoggerService>();

// Register UnitOfWork
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(CosmosRepository<>));

builder.ConfigureFunctionsWebApplication();

var host = builder.Build();

// Force initialization of CosmosDbContext on startup
using (var scope = host.Services.CreateScope())
{
    var cosmosContext = scope.ServiceProvider.GetRequiredService<CosmosDbContext>();
}

host.Run();
