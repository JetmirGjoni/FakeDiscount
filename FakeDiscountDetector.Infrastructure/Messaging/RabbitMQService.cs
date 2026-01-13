using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Configurations;
using FakeDiscountDetector.Infrastructure.Scraping;
using FakeDiscountDetector.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FakeDiscountDetector.Infrastructure.Messaging
{
    public class RabbitMQService : IAsyncDisposable, IMessageQueueService
    {
        private readonly ILogger<RabbitMQService> _logger;
        private readonly string _hostname;
        private IConnection? _connection;
        private IChannel? _channel;
        private const string QUEUE_NAME = "scraping_tasks";

        public RabbitMQService(IConfiguration config, ILogger<RabbitMQService> logger)
        {
            _logger = logger;
            _hostname = config["RabbitMQ:Hostname"] ?? "localhost";
        }

        private async Task InitializeRabbitMQAsync()
        {
            if (_channel != null && _channel.IsOpen) return;

            try
            {
                var factory = new ConnectionFactory { HostName = _hostname };
                _connection = await factory.CreateConnectionAsync();
                _channel = await _connection.CreateChannelAsync();

                await _channel.QueueDeclareAsync(queue: QUEUE_NAME,
                                     durable: false,
                                     exclusive: false,
                                     autoDelete: false,
                                     arguments: null);

                _logger.LogInformation("Connected to RabbitMQ at {Hostname}", _hostname);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not connect to RabbitMQ");
            }
        }

        public async Task PublishScrapingTaskAsync(ScraperConfig config)
        {
            await InitializeRabbitMQAsync();
            if (_channel == null) return;

            var json = JsonSerializer.Serialize(config);
            var body = Encoding.UTF8.GetBytes(json);

            var props = new BasicProperties();
            await _channel.BasicPublishAsync(exchange: "",
                                 routingKey: QUEUE_NAME,
                                 mandatory: false,
                                 basicProperties: props,
                                 body: body);

            _logger.LogInformation("Published scraping task for {Store}", config.Name);
        }

        public async Task ConsumeScrapingTasksAsync(Func<ScraperConfig, Task> onMessage, CancellationToken cancellationToken)
        {
            await InitializeRabbitMQAsync();
            if (_channel == null) return;

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (model, ea) =>
            {
                var body = ea.Body.ToArray();
                var json = Encoding.UTF8.GetString(body);
                try
                {
                    var config = JsonSerializer.Deserialize<ScraperConfig>(json);
                    if (config != null)
                    {
                        await onMessage(config);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing message");
                }
            };

            await _channel.BasicConsumeAsync(queue: QUEUE_NAME,
                                 autoAck: true,
                                 consumer: consumer,
                                 cancellationToken: cancellationToken);

            // Keep alive mechanism
            try
            {
                await Task.Delay(-1, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                // Expected on shutdown
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (_channel != null) await _channel.CloseAsync();
            if (_connection != null) await _connection.CloseAsync();
        }
    }
}
