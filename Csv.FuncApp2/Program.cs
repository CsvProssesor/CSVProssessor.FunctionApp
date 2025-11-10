using Csv.FuncApp2.IOContainer;
using CSVProssessor.Application.Interfaces;
using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Domain.DTOs.EmailDTOs;
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

host.Run();