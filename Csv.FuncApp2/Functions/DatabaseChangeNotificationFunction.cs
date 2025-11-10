using System.Text;
using System.Text.Json;
using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Domain.DTOs.EmailDTOs;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Csv.FuncApp2.Functions;

public class DatabaseChangeNotificationFunction
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IEmailService _emailService;
    private readonly ILogger<DatabaseChangeNotificationFunction> _logger;
    private readonly ILoggerService _loggerService;

    public DatabaseChangeNotificationFunction(
        ILogger<DatabaseChangeNotificationFunction> logger,
        ILoggerService loggerService,
        IEmailService emailService,
        IConnectionFactory connectionFactory)
    {
        _logger = logger;
        _loggerService = loggerService;
        _emailService = emailService;
        _connectionFactory = connectionFactory;
    }

    [Function("DatabaseChangeNotificationFunction")]
    public async Task RunAsync([TimerTrigger("0 */1 * * * *")] TimerInfo myTimer)
    {
        _logger.LogInformation("DatabaseChangeNotificationFunction started at {time}", DateTime.UtcNow);

        try
        {
            // Subscribe to topic and listen for messages
            await SubscribeAndProcessAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in DatabaseChangeNotificationFunction");
            _loggerService.Error($"[FuncApp2] Error in DatabaseChangeNotificationFunction: {ex.Message}");
        }
    }

    private async Task SubscribeAndProcessAsync()
    {
        try
        {
            using var connection = await _connectionFactory.CreateConnectionAsync();
            var channel = await connection.CreateChannelAsync();

            // Declare exchange
            await channel.ExchangeDeclareAsync(
                "csv-changes-topic",
                ExchangeType.Fanout,
                true,
                false);

            // Create temporary queue
            var queueDeclareOk = await channel.QueueDeclareAsync(
                string.Empty,
                false,
                true,
                true);

            var queueName = queueDeclareOk.QueueName;

            // Bind queue to exchange
            await channel.QueueBindAsync(
                queueName,
                "csv-changes-topic",
                string.Empty);

            // Set QoS
            await channel.BasicQosAsync(0, 1, false);

            // Create consumer
            var consumer = new AsyncEventingBasicConsumer(channel);
            var messageReceived = false;

            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    // Get message content
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    _logger.LogInformation("[FuncApp2] Received change notification: {message}", json);
                    _loggerService.Info($"[FuncApp2] Received change notification: {json}");

                    // Send email from FuncApp2
                    var emailRequest = new EmailRequestDto
                    {
                        To = "phuctg1@fpt.com"
                    };

                    await _emailService.SendDatabaseChangesWithSource(emailRequest, "FuncApp2 - Notification & Monitoring");
                    _loggerService.Success("[FuncApp2] Email sent successfully for database change");

                    // Acknowledge message
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                    messageReceived = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "[FuncApp2] Error processing message");
                    _loggerService.Error($"[FuncApp2] Error processing message: {ex.Message}");
                    // Negative acknowledge - requeue
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            // Start consuming
            await channel.BasicConsumeAsync(
                queueName,
                false,
                $"FuncApp2-{Guid.NewGuid():N}",
                consumer);

            _logger.LogInformation("[FuncApp2] Started listening to csv-changes-topic");

            // Wait for a short time to receive messages (10 seconds)
            await Task.Delay(10000);

            if (messageReceived)
            {
                _logger.LogInformation("[FuncApp2] Message processed successfully");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[FuncApp2] Error subscribing to topic");
            _loggerService.Error($"[FuncApp2] Error subscribing to topic: {ex.Message}");
        }
    }
}
