using System.Text;
using System.Text.Json;
using CSVProssessor.Application.Interfaces.Common;
using CSVProssessor.Domain.DTOs.EmailDTOs;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CSVProssessor.Application.Worker;

/// <summary>
///     Background service to listen for database changes from Cosmos DB
///     and send email notifications when changes are detected
/// </summary>
public class DatabaseChangeNotificationService : BackgroundService
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseChangeNotificationService(IServiceProvider serviceProvider, IConnectionFactory connectionFactory)
    {
        _serviceProvider = serviceProvider;
        _connectionFactory = connectionFactory;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerService>();

        try
        {
            // Tạo connection từ factory
            using var connection = await _connectionFactory.CreateConnectionAsync();

            // Tạo channel từ connection
            var channel = await connection.CreateChannelAsync();

            // Khai báo exchange kiểu fanout
            await channel.ExchangeDeclareAsync(
                "csv-changes-topic",
                ExchangeType.Fanout,
                true,
                false);

            // Tạo queue tạm thời (exclusive, auto-delete)
            var queueDeclareOk = await channel.QueueDeclareAsync(
                string.Empty,
                false,
                true,
                true);

            var queueName = queueDeclareOk.QueueName;

            // Bind queue vào exchange
            await channel.QueueBindAsync(
                queueName,
                "csv-changes-topic",
                string.Empty);

            // Thiết lập QoS
            await channel.BasicQosAsync(0, 1, false);

            // Tạo consumer để xử lý message
            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += OnMessageReceived;

            // Hàm async riêng để xử lý message
            async Task OnMessageReceived(object model, BasicDeliverEventArgs ea)
            {
                // Create a new scope for each message to resolve scoped services
                using var messageScope = _serviceProvider.CreateScope();
                var emailService = messageScope.ServiceProvider.GetRequiredService<IEmailService>();
                var scopedLogger = messageScope.ServiceProvider.GetRequiredService<ILoggerService>();

                try
                {
                    // Giải mã message từ byte array
                    var json = Encoding.UTF8.GetString(ea.Body.ToArray());
                    scopedLogger.Info($"[DatabaseChangeNotification] Received change notification: {json}");

                    // Send email notification
                    var emailRequest = new EmailRequestDto
                    {
                        To = "phuctg1@fpt.com"
                    };

                    await emailService.SendDatabaseChanges(emailRequest);
                    scopedLogger.Success($"[DatabaseChangeNotification] Email sent successfully for database change");

                    // ACK - xác nhận đã xử lý thành công
                    await channel.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    scopedLogger.Error($"[DatabaseChangeNotification] Error sending email notification: {ex}");
                    // NACK và requeue - đưa message trở lại queue để retry
                    await channel.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            }

            // Bắt đầu consume message từ queue
            await channel.BasicConsumeAsync(
                queueName,
                false,
                $"DatabaseChangeNotification-{Guid.NewGuid():N}",
                consumer);

            logger.Info("Started listening to csv-changes-topic for database change notifications");

            // Giữ service chạy cho đến khi bị cancel
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (Exception ex)
        {
            using var errorScope = _serviceProvider.CreateScope();
            var errorLogger = errorScope.ServiceProvider.GetRequiredService<ILoggerService>();
            errorLogger.Error($"[DatabaseChangeNotification] Fatal error: {ex}");
            throw;
        }
    }
}
