using CSVProssessor.Application.Interfaces;
using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Application.Services;
using CSVProssessor.Application.Services.Common;
using CSVProssessor.Application.Worker;
using CSVProssessor.Domain;
using CSVProssessor.Infrastructure;
using CSVProssessor.Infrastructure.Commons;
using CSVProssessor.Infrastructure.Interfaces;
using CSVProssessor.Infrastructure.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;

namespace Csv.FuncApp1.IOContainer;

public static class IocContainer
{
    public static IServiceCollection SetupIocContainer(this IServiceCollection services)
    {
        //Add Logger

        //Add business services
        services.SetupBusinessServicesLayer();

        //Add HttpContextAccessor for role-based checks
        services.AddHttpContextAccessor();
        services.SetupCosmosDb();

        //services.SetupJwt();
        // services.SetupGraphQl();
        return services;
    }

    public static IServiceCollection SetupBusinessServicesLayer(this IServiceCollection services)
    {
        services.AddScoped(typeof(IGenericRepository<>), typeof(CosmosRepository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IClaimsService, ClaimsService>();
        services.AddScoped<ICurrentTime, CurrentTime>();
        services.AddScoped<ILoggerService, LoggerService>();

        services.AddScoped<IBlobService, BlobService>();
        services.AddScoped<IRabbitMqService, RabbitMqService>();
        services.AddScoped<ICsvService, CsvService>();

        // Register BackgroundServices
        services.AddHostedService<CsvImportQueueListenerService>();
        //services.AddHostedService<ChangeDetectionBackgroundService>();

        // Configure RabbitMQ Connection Factory (not connection itself)
        services.AddSingleton<IConnectionFactory>(sp =>
        {
            var configuration = sp.GetRequiredService<IConfiguration>();
            var rabbitmqHost = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ??
                               configuration["RabbitMQ:Host"] ?? "localhost";
            var rabbitmqUser = Environment.GetEnvironmentVariable("RABBITMQ_USER") ??
                               configuration["RabbitMQ:User"] ?? "guest";
            var rabbitmqPassword = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ??
                                   configuration["RabbitMQ:Password"] ?? "guest";
            var rabbitmqPort = int.Parse(Environment.GetEnvironmentVariable("RABBITMQ_PORT") ??
                                         configuration["RabbitMQ:Port"] ?? "5672");

            var factory = new ConnectionFactory
            {
                HostName = rabbitmqHost,
                UserName = rabbitmqUser,
                Password = rabbitmqPassword,
                Port = rabbitmqPort,
                AutomaticRecoveryEnabled = true,
                RequestedConnectionTimeout = TimeSpan.FromSeconds(30),
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10),
                RequestedHeartbeat = TimeSpan.FromSeconds(60),
                ConsumerDispatchConcurrency = 1
            };

            return factory;
        });

        services.AddHttpContextAccessor();

        return services;
    }

    public static IServiceCollection SetupCosmosDb(this IServiceCollection services)
    {
        var connectionString = Environment.GetEnvironmentVariable("CosmosDbConnectionString");
        if (string.IsNullOrEmpty(connectionString))
            throw new InvalidOperationException("CosmosDbConnectionString is not configured");
        services.AddSingleton(sp =>
        {
            var context = new CosmosDbContext(connectionString);
            var initTask = context.InitializeAsync("CSVProcessor");
            initTask.GetAwaiter().GetResult();
            return context;
        });
        return services;
    }
}