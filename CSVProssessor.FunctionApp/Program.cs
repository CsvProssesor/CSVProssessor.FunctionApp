using CSVProssessor.FunctionApp1.Architecture;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;

var builder = FunctionsApplication.CreateBuilder(args);

builder.Services.SetupIocContainer();

builder.ConfigureFunctionsWebApplication();

var host = builder.Build();

host.Run();