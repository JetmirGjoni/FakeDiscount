using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Configurations;
using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Infrastructure.Scraping;
using Microsoft.Extensions.Logging;

namespace FakeDiscountDetector.Infrastructure.Messaging
{
    public class InMemoryMessageQueueService : IMessageQueueService
    {
        private readonly Channel<ScraperConfig> _channel;
        private readonly ILogger<InMemoryMessageQueueService> _logger;

        public InMemoryMessageQueueService(ILogger<InMemoryMessageQueueService> logger)
        {
            _logger = logger;
            // Unbounded channel for simplicity in testing, or bounded to test backpressure
            _channel = Channel.CreateUnbounded<ScraperConfig>();
        }

        public async Task PublishScrapingTaskAsync(ScraperConfig config)
        {
            await _channel.Writer.WriteAsync(config);
            _logger.LogInformation("In-Memory: Published task for {Store}", config.Name);
        }

        public async Task ConsumeScrapingTasksAsync(Func<ScraperConfig, Task> onMessage, CancellationToken cancellationToken)
        {
            try
            {
                await foreach (var config in _channel.Reader.ReadAllAsync(cancellationToken))
                {
                    await onMessage(config);
                }
            }
            catch (OperationCanceledException)
            {
                // Graceful shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in in-memory consumer");
            }
        }
    }
}
