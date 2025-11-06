using CSVProssessor.Infrastructure.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace CSVProssessor.Infrastructure.Commons
{
    /// <summary>
    /// Dịch vụ RabbitMQ - Xử lý gửi và nhận message từ message broker RabbitMQ
    /// </summary>
    public class RabbitMqService : IRabbitMqService
    {
        // Connection factory - để tạo connection on-demand
        private readonly IConnectionFactory _connectionFactory;

        // Lazy connection - chỉ tạo khi cần
        private IConnection _connection;

        // Channel - Kênh giao tiếp để gửi/nhận message
        private IChannel _channel;

        // Logger - Ghi log hoạt động của dịch vụ
        private readonly ILogger<RabbitMqService> _logger;

        private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

        /// <summary>
        /// Constructor - Khởi tạo kết nối và channel khi dịch vụ được tạo
        /// </summary>
        public RabbitMqService(IConnectionFactory connectionFactory, ILogger<RabbitMqService> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        /// <summary>
        /// Đảm bảo connection và channel đã được khởi tạo
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
                    _logger.LogInformation("Attempting to connect to RabbitMQ...");
                    _connection = await _connectionFactory.CreateConnectionAsync();
                    _logger.LogInformation("RabbitMQ connection created successfully");

                    _channel = await _connection.CreateChannelAsync();
                    _logger.LogInformation("RabbitMQ channel created successfully");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Failed to connect to RabbitMQ: {ex.Message}");
                    throw;
                }
            }
            finally
            {
                _connectionLock.Release();
            }
        }

        /// <summary>
        /// Gửi message tới queue cụ thể
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
                // durable: true - Queue sẽ tồn tại ngay cả khi broker restart
                // exclusive: false - Có thể được shared bởi nhiều consumer
                // autoDelete: false - Queue không tự xóa khi consumer disconnect
                await _channel.QueueDeclareAsync(
                    queue: destination,
                    durable: true,              // Queue tồn tại sau khi broker restart
                    exclusive: false,           // Có thể dùng chung
                    autoDelete: false,          // Không tự xóa
                    arguments: null
                );

                // Cấu hình thuộc tính message
                var properties = new BasicProperties
                {
                    Persistent = true,           // Lưu message vào disk (không mất khi restart)
                    ContentType = "application/json", // Định dạng message là JSON
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) // Thời gian gửi
                };

                // Gửi message tới queue
                // exchange: string.Empty - Gửi thẳng vào queue (không qua exchange)
                // routingKey: destination - Tên queue là routing key
                await _channel.BasicPublishAsync(
                    exchange: string.Empty,     // Gửi trực tiếp, không qua exchange
                    routingKey: destination,    // Tên queue là routing key
                    mandatory: false,
                    basicProperties: properties,
                    body: new ReadOnlyMemory<byte>(body)
                );

                // Ghi log thành công
                _logger.LogInformation($"Message published successfully to queue '{destination}'. Message: {jsonMessage}");
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu có vấn đề
                _logger.LogError(ex, $"Error publishing message to RabbitMQ queue '{destination}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Gửi message tới topic (exchange kiểu fanout) - Phát tán cho nhiều subscriber
        /// </summary>
        /// <typeparam name="T">Kiểu dữ liệu của message</typeparam>
        /// <param name="topicName">Tên của topic (exchange)</param>
        /// <param name="message">Đối tượng message cần gửi (sẽ được chuyển thành JSON)</param>
        public async Task PublishToTopicAsync<T>(string topicName, T message)
        {
            try
            {
                // Chuyển đổi message thành chuỗi JSON
                var jsonMessage = JsonSerializer.Serialize(message);
                var body = Encoding.UTF8.GetBytes(jsonMessage);

                // Khai báo exchange kiểu fanout
                // fanout: Gửi message tới tất cả các queue đã bind vào exchange này
                // durable: true - Exchange sẽ tồn tại ngay cả khi broker restart
                await _channel.ExchangeDeclareAsync(
                    exchange: topicName,
                    type: ExchangeType.Fanout, // Fanout - phát tán cho tất cả subscriber
                    durable: true,              // Exchange tồn tại sau khi broker restart
                    autoDelete: false,          // Exchange không tự xóa
                    arguments: null
                );

                // Cấu hình thuộc tính message
                var properties = new BasicProperties
                {
                    Persistent = true,           // Lưu message vào disk (không mất khi restart)
                    ContentType = "application/json", // Định dạng message là JSON
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()) // Thời gian gửi
                };

                // Gửi message tới topic (exchange)
                // exchange: topicName - Gửi tới exchange này
                // routingKey: string.Empty - Fanout không cần routing key, gửi cho tất cả queue bind vào
                await _channel.BasicPublishAsync(
                    exchange: topicName,
                    routingKey: string.Empty,  // Fanout gửi cho tất cả, không cần routing
                    mandatory: false,
                    basicProperties: properties,
                    body: new ReadOnlyMemory<byte>(body)
                );

                // Ghi log thành công
                _logger.LogInformation($"Message published successfully to topic '{topicName}'. Message: {jsonMessage}");
            }
            catch (Exception ex)
            {
                // Ghi log lỗi nếu có vấn đề
                _logger.LogError(ex, $"Error publishing message to RabbitMQ topic '{topicName}': {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Giải phóng tài nguyên - Đóng channel và kết nối
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
}