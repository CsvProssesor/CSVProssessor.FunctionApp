using Csv.FuncApp1.IOContainer;
using CSVProssessor.Application.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);
builder.Services.SetupIocContainer();

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

var host = builder.Build();

using (var scope = host.Services.CreateScope())
{
    var csvService = scope.ServiceProvider.GetRequiredService<ICsvService>();
    _ = csvService.LogCsvChangesAsync();
}

host.Run();