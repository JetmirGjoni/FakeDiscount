using System;
using System.Threading;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Configurations;
using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Infrastructure.Scraping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FakeDiscountDetector.Worker
{
    using FakeDiscountDetector.Infrastructure.Messaging;

    public partial class ScrapingWorker(ILogger<ScrapingWorker> logger, IServiceProvider serviceProvider, IMessageQueueService queueService) : BackgroundService
    {
        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            logger.LogInformation("ScrapingWorker: Waiting for tasks...");


            var task = Task.Run(async () =>
            {
                await queueService.ConsumeScrapingTasksAsync(async (config) =>
                {
                    logger.LogInformation("Received task for {Store}", config.Name);
                    await ProcessScrapingTask(config);
                }, stoppingToken);
            }, stoppingToken);

            return Task.CompletedTask;
        }

        private async Task ProcessScrapingTask(ScraperConfig config)
        {
            using (var scope = serviceProvider.CreateScope())
            {
                var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                var analyzer = scope.ServiceProvider.GetRequiredService<IDiscountAnalyzer>();
                var classifier = scope.ServiceProvider.GetRequiredService<IProductClassifier>();

                // Instantiate the scraper dynamically
                var scraper = new GenericConfigurableScraper(config);

                try
                {
                    // Crawling Logic: If CategorySelector is present and TargetUrl is null, we discover
                    if (!string.IsNullOrEmpty(config.CategorySelector) && string.IsNullOrEmpty(config.TargetUrl))
                    {
                        logger.LogInformation("[{Store}] Discovery mode: Finding categories...", config.Name);
                        var categories = await scraper.DiscoverCategoriesAsync();
                        foreach (var categoryUrl in categories)
                        {
                            var subTaskConfig = new ScraperConfig
                            {
                                Name = config.Name,
                                BaseUrl = config.BaseUrl,
                                TargetUrl = categoryUrl,
                                CategorySelector = null, // Important: prevent infinite loop
                                ItemSelector = config.ItemSelector,
                                NameSelector = config.NameSelector,
                                PriceSelector = config.PriceSelector,
                                OldPriceSelector = config.OldPriceSelector,
                                ImageSelector = config.ImageSelector,
                                PaginationType = config.PaginationType,
                                PaginationSelector = config.PaginationSelector,
                                MaxPages = config.MaxPages,
                                WaitSelector = config.WaitSelector,
                                PriceMultiplier = config.PriceMultiplier,
                                OldPriceMultiplier = config.OldPriceMultiplier,
                                PriceCulture = config.PriceCulture
                            };
                            await queueService.PublishScrapingTaskAsync(subTaskConfig);
                        }
                        logger.LogInformation("[{Store}] Discovery complete. Published {Count} sub-tasks.", config.Name, categories.Count);
                        return;
                    }

                    LogStartingScraping(logger, config.Name);
                    var products = await scraper.ScrapeAsync();
                    LogScrapedProducts(logger, products.Count, config.Name);

                    for (int i = 0; i < products.Count; i++)
                    {
                        var product = products[i];
                        logger.LogInformation("Processing product {Index}/{Total}: {Name}", i + 1, products.Count, product.Name);

                        var existingProduct = await repository.GetProductByUrlAsync(product.Url);
                        if (existingProduct == null)
                        {
                            logger.LogInformation("Product not found in DB. Predicting category...");
                            product.Category = await classifier.PredictCategoryAsync(product);

                            logger.LogInformation("Adding new product to DB...");
                            await repository.AddProductAsync(product);
                            LogAddedNewProduct(logger, product.Name, product.Category);
                        }
                        else
                        {
                            logger.LogInformation("Product exists (ID: {Id}). Processing price history...", existingProduct.Id);

                            // Check for reclassification need
                            if (string.IsNullOrEmpty(existingProduct.Category) || existingProduct.Category == "Other" || existingProduct.Category == "Uncategorized")
                            {
                                logger.LogInformation("Product category is '{Category}'. Re-classifying...", existingProduct.Category ?? "null");
                                existingProduct.Category = await classifier.PredictCategoryAsync(product);
                                await repository.UpdateProductAsync(existingProduct);
                                logger.LogInformation("Product updated with new category: {Category}", existingProduct.Category);
                            }

                            var latestPrice = product.PriceHistory.FirstOrDefault();
                            if (latestPrice != null)
                            {
                                latestPrice.ProductId = existingProduct.Id;

                                logger.LogInformation("Adding price record to DB...");
                                await repository.AddPriceRecordAsync(latestPrice);
                                logger.LogInformation("Price record added.");

                                var isFake = analyzer.IsFakeDiscount(existingProduct, latestPrice.Price, latestPrice.OriginalPrice);
                                if (isFake)
                                {
                                    LogPotentialFakeDiscount(logger, product.Name, latestPrice.Price, latestPrice.OriginalPrice);
                                }
                                else
                                {
                                    LogPriceUpdate(logger, product.Name, latestPrice.Price);
                                }
                            }
                        }
                        logger.LogInformation("Finished processing product {Index}", i + 1);
                    }

                    logger.LogInformation("[{Store}] Scraping task completely finished.", config.Name);
                }
                catch (Exception ex)
                {
                    LogErrorScraping(logger, config.Name, ex);
                }
            }
        }

        [LoggerMessage(Level = LogLevel.Information, Message = "Worker running at: {time}")]
        static partial void LogWorkerRunning(ILogger logger, DateTimeOffset time);

        [LoggerMessage(Level = LogLevel.Information, Message = "Starting scraping with {ScraperName}...")]
        static partial void LogStartingScraping(ILogger logger, string scraperName);

        [LoggerMessage(Level = LogLevel.Information, Message = "{ScraperName} scraped {Count} products.")]
        static partial void LogScrapedProducts(ILogger logger, int count, string scraperName);

        [LoggerMessage(Level = LogLevel.Information, Message = "Added new product: {Name} [{Category}]")]
        static partial void LogAddedNewProduct(ILogger logger, string name, string category);

        [LoggerMessage(Level = LogLevel.Warning, Message = "POTENTIAL FAKE DISCOUNT DETECTED: {Name}. Price: {Price}, Claimed Original: {OriginalPrice}")]
        static partial void LogPotentialFakeDiscount(ILogger logger, string name, decimal price, decimal? originalPrice);

        [LoggerMessage(Level = LogLevel.Information, Message = "Updated price for {Name}. New Price: {Price}")]
        static partial void LogPriceUpdate(ILogger logger, string name, decimal price);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error during scraping cycle for {ScraperName}.")]
        static partial void LogErrorScraping(ILogger logger, string scraperName, Exception ex);
    }
}
