using System;
using System.Threading;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Infrastructure.Scraping;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FakeDiscountDetector.Worker
{
    public partial class ScrapingWorker(ILogger<ScrapingWorker> logger, IServiceProvider serviceProvider) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                LogWorkerRunning(logger, DateTimeOffset.Now);

                using (var scope = serviceProvider.CreateScope())
                {
                    var scrapers = scope.ServiceProvider.GetRequiredService<IEnumerable<IScraper>>();
                    var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                    var analyzer = scope.ServiceProvider.GetRequiredService<IDiscountAnalyzer>();
                    var classifier = scope.ServiceProvider.GetRequiredService<IProductClassifier>();

                    foreach (var scraper in scrapers)
                    {
                        try
                        {
                            LogStartingScraping(logger, scraper.GetType().Name);
                            var products = await scraper.ScrapeAsync();
                            LogScrapedProducts(logger, products.Count, scraper.GetType().Name);

                            foreach (var product in products)
                            {
                                var existingProduct = await repository.GetProductByUrlAsync(product.Url);
                                if (existingProduct == null)
                                {
                                    // Classify product
                                    product.Category = await classifier.PredictCategoryAsync(product);

                                    await repository.AddProductAsync(product);
                                    LogAddedNewProduct(logger, product.Name, product.Category);
                                }
                                else
                                {
                                    // Add price record
                                    var latestPrice = product.PriceHistory.FirstOrDefault();
                                    if (latestPrice != null)
                                    {
                                        latestPrice.ProductId = existingProduct.Id;
                                        await repository.AddPriceRecordAsync(latestPrice);

                                        // Check for fake discount
                                        var isFake = analyzer.IsFakeDiscount(existingProduct, latestPrice.Price, latestPrice.OriginalPrice);
                                        if (isFake)
                                        {
                                            LogPotentialFakeDiscount(logger, product.Name, latestPrice.Price, latestPrice.OriginalPrice);
                                        }
                                    }
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            LogErrorScraping(logger, scraper.GetType().Name, ex);
                        }
                    }

                }

                await Task.Delay(TimeSpan.FromHours(3), stoppingToken); //  10 sec for debugging
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

        [LoggerMessage(Level = LogLevel.Error, Message = "Error during scraping cycle for {ScraperName}.")]
        static partial void LogErrorScraping(ILogger logger, string scraperName, Exception ex);
    }
}
