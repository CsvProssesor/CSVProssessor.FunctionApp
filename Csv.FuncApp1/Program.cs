using Csv.FuncApp1.IOContainer;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections;

var builder = FunctionsApplication.CreateBuilder(args);
builder.Services.SetupIocContainer();

builder.ConfigureFunctionsWebApplication();

builder.Services
    .AddApplicationInsightsTelemetryWorkerService()
    .ConfigureFunctionsApplicationInsights();

// Bổ sung Console logger
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
});

var host = builder.Build();

var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("EnvLogger");

// Log tất cả biến môi trường
logger.LogInformation("=== Environment Variables ===");
foreach (DictionaryEntry kvp in Environment.GetEnvironmentVariables())
{
    logger.LogInformation("{Key} = {Value}", kvp.Key, kvp.Value);
}
logger.LogInformation("=== End of Environment Variables ===");

host.Run();
