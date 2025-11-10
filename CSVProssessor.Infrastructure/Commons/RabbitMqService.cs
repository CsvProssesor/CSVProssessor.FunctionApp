using System.Text;
using System.Text.Json;
using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace CSVProssessor.Infrastructure.Commons;

/// <summary>
///     Dịch vụ RabbitMQ - Xử lý gửi và nhận message từ message broker RabbitMQ
/// </summary>
public class RabbitMqService : IRabbitMqService
{
    // Connection factory - để tạo connection on-demand
    private readonly IConnectionFactory _connectionFactory;

    private readonly SemaphoreSlim _connectionLock = new(1, 1);

    // Logger - Ghi log hoạt động của dịch vụ
    private readonly ILogger<RabbitMqService> _logger;

    // Channel - Kênh giao tiếp để gửi/nhận message
    private IChannel? _channel;

    // Lazy connection - chỉ tạo khi cần
    private IConnection? _connection;

    /// <summary>
    ///     Constructor - Khởi tạo kết nối và channel khi dịch vụ được tạo
    /// </summary>
    public RabbitMqService(IConnectionFactory connectionFactory, ILogger<RabbitMqService> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Gửi message tới queue cụ thể
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của message</typeparam>
    /// <param name="destination">Tên queue hoặc exchange muốn gửi tới</param>
    /// <param name="message">Đối tượng message cần gửi (sẽ được chuyển thành JSON)</param>
    public async Task PublishAsync<T>(string destination, T message)
    {
        try
        {
            // Đảm bảo connection và channel đã khởi tạo
            await EnsureConnectionAsync();

            // Chuyển đổi message thành chuỗi JSON
            var jsonMessage = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(jsonMessage);

            // Khai báo queue với durable option
            await _channel!.QueueDeclareAsync(
                destination,
                true, // Queue tồn tại sau khi broker restart
                false, // Có thể dùng chung
                false
            );

            // Cấu hình thuộc tính message
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Gửi message tới queue
            await _channel!.BasicPublishAsync(
                string.Empty,
                destination,
                false,
                properties,
                new ReadOnlyMemory<byte>(body)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error publishing to queue '{destination}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Gửi message tới topic (exchange kiểu fanout) - Phát tán cho nhiều subscriber
    /// </summary>
    /// <typeparam name="T">Kiểu dữ liệu của message</typeparam>
    /// <param name="topicName">Tên của topic (exchange)</param>
    /// <param name="message">Đối tượng message cần gửi (sẽ được chuyển thành JSON)</param>
    public async Task PublishToTopicAsync<T>(string topicName, T message)
    {
        try
        {
            // Đảm bảo connection và channel đã khởi tạo
            await EnsureConnectionAsync();

            // Chuyển đổi message thành chuỗi JSON
            var jsonMessage = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(jsonMessage);

            // Khai báo exchange kiểu fanout
            await _channel!.ExchangeDeclareAsync(
                topicName,
                ExchangeType.Fanout,
                true,
                false
            );

            // Cấu hình thuộc tính message
            var properties = new BasicProperties
            {
                Persistent = true,
                ContentType = "application/json",
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            // Gửi message tới topic
            await _channel!.BasicPublishAsync(
                topicName,
                string.Empty,
                false,
                properties,
                new ReadOnlyMemory<byte>(body)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error publishing to topic '{topicName}': {ex.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Subscribe tới topic (fanout exchange) và lắng nghe message từ các publisher
    /// </summary>
    /// <param name="topicName">Tên của topic (exchange)</param>
    /// <param name="handler">Hàm xử lý message nhận được (nhận vào message dạng string JSON)</param>
    public async Task SubscribeToTopicAsync(string topicName, Func<string, Task> handler)
    {
        try
        {
            // Đảm bảo connection và channel đã khởi tạo
            await EnsureConnectionAsync();

            // Khai báo exchange kiểu fanout
            await _channel!.ExchangeDeclareAsync(
                topicName,
                ExchangeType.Fanout,
                true,
                false
            );

            // Tạo queue tạm thời (auto-generated name)
            var queueDeclareOk = await _channel!.QueueDeclareAsync(
                string.Empty,
                false,
                true,
                true
            );

            var queueName = queueDeclareOk.QueueName;

            // Bind queue vào exchange
            await _channel!.QueueBindAsync(
                queueName,
                topicName,
                string.Empty
            );

            // Tạo consumer để lắng nghe message
            var consumer = new AsyncEventingBasicConsumer(_channel!);

            // Đăng ký handler khi nhận được message
            consumer.ReceivedAsync += async (model, ea) =>
            {
                try
                {
                    // Lấy nội dung message từ body
                    var body = ea.Body.ToArray();
                    var messageJson = Encoding.UTF8.GetString(body);

                    // Gọi handler để xử lý message
                    await handler(messageJson);

                    // Acknowledge message
                    await _channel!.BasicAckAsync(ea.DeliveryTag, false);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing message from topic '{topicName}'");

                    // Negative acknowledge - message sẽ được requeue
                    await _channel!.BasicNackAsync(ea.DeliveryTag, false, true);
                }
            };

            // Bắt đầu consume message từ queue
            await _channel!.BasicConsumeAsync(
                queueName,
                false,
                $"{topicName}-subscriber-{Guid.NewGuid():N}",
                consumer
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error subscribing to topic '{topicName}'");
            throw;
        }
    }


    /// <summary>
    ///     Đảm bảo connection và channel đã được khởi tạo
    /// </summary>
    private async Task EnsureConnectionAsync()
    {
        if (_channel != null && !_channel.IsClosed)
            return;

        await _connectionLock.WaitAsync();
        try
        {
            if (_channel != null && !_channel.IsClosed)
                return;

            try
            {
                _connection = await _connectionFactory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to RabbitMQ");
                throw;
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    /// <summary>
    ///     Giải phóng tài nguyên - Đóng channel và kết nối
    /// </summary>
    public void Dispose()
    {
        // Đóng channel - kết nối giao tiếp
        _channel?.Dispose();
        // Đóng kết nối tới RabbitMQ
        _connection?.Dispose();
        // Giải phóng semaphore
        _connectionLock?.Dispose();
    }
}