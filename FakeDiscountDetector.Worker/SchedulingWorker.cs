using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Core.Configurations;
using FakeDiscountDetector.Infrastructure.Messaging;
using FakeDiscountDetector.Infrastructure.Scraping;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FakeDiscountDetector.Worker
{
    public class SchedulingWorker : BackgroundService
    {
        private readonly ILogger<SchedulingWorker> _logger;
        private readonly IMessageQueueService _queueService;

        public SchedulingWorker(ILogger<SchedulingWorker> logger, IMessageQueueService queueService)
        {
            _logger = logger;
            _queueService = queueService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("SchedulingWorker: Starting scheduling cycle...");

                // Use test scrapers for debugging
                var scrapersJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "../FakeDiscountDetector.Infrastructure/Scraping/scrapers_test.json");

                if (!File.Exists(scrapersJsonPath))
                {
                    scrapersJsonPath = Path.Combine(Directory.GetCurrentDirectory(), "../FakeDiscountDetector.Infrastructure/Scraping/scrapers.json");
                }

                if (!File.Exists(scrapersJsonPath))
                {
                    scrapersJsonPath = "scrapers.json";
                }

                if (File.Exists(scrapersJsonPath))
                {
                    try
                    {
                        var json = await File.ReadAllTextAsync(scrapersJsonPath, stoppingToken);
                        var configs = JsonSerializer.Deserialize<List<ScraperConfig>>(json);

                        if (configs != null)
                        {
                            foreach (var config in configs)
                            {
                                _logger.LogInformation("Scheduling scraping for {Store}", config.Name);
                                await _queueService.PublishScrapingTaskAsync(config);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error reading scrapers.json");
                    }
                }
                else
                {
                    _logger.LogWarning("scrapers.json not found!");
                }

                // Schedule every 3 hours
                await Task.Delay(TimeSpan.FromHours(3), stoppingToken);
            }
        }
    }
}
