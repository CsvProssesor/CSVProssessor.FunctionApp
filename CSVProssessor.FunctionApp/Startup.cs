using CSVProssessor.Domain;
using CSVProssessor.Infrastructure;
using CSVProssessor.Infrastructure.Commons;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(CSVProssessor.FunctionApp.Startup))]

namespace CSVProssessor.FunctionApp;

public class Startup : FunctionsStartup
{
    public override void Configure(IFunctionsHostBuilder builder)
    {
        var connectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString") 
            ?? throw new InvalidOperationException("CosmosDbConnectionString is not configured");

        // Register CosmosDbContext as singleton
        builder.Services.AddSingleton(sp =>
        {
            var context = new CosmosDbContext(connectionString);
            // Initialize database and containers on startup
            context.InitializeAsync("CSVProcessor").GetAwaiter().GetResult();
            return context;
        });

        // Register common services
        builder.Services.AddScoped<ICurrentTime, CurrentTime>();
        builder.Services.AddScoped<IClaimsService, ClaimsService>();
        builder.Services.AddScoped<ILoggerService, LoggerService>();

        // Register UnitOfWork
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
    }
}
