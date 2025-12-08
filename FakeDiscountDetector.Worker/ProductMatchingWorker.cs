using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FakeDiscountDetector.Worker
{
    public class ProductMatchingWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ProductMatchingWorker> _logger;

        public ProductMatchingWorker(IServiceProvider serviceProvider, ILogger<ProductMatchingWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogInformation("ProductMatchingWorker running at: {time}", DateTimeOffset.Now);

                using (var scope = _serviceProvider.CreateScope())
                {
                    var repository = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                    var matcher = scope.ServiceProvider.GetRequiredService<IProductMatcher>();

                    var ungroupedProducts = await repository.GetUngroupedProductsAsync();
                    var allProducts = await repository.GetAllProductsAsync(); // In a real app, we might optimize this

                    foreach (var product in ungroupedProducts)
                    {
                        bool matchFound = false;

                        // Try to find a match in existing groups
                        foreach (var candidate in allProducts.Where(p => p.GroupId != null && p.Id != product.Id))
                        {
                            if (matcher.AreMatch(product, candidate))
                            {
                                product.GroupId = candidate.GroupId;
                                await repository.UpdateProductAsync(product);
                                _logger.LogInformation($"Matched {product.Name} with {candidate.Name} (Group: {candidate.GroupId})");
                                matchFound = true;
                                break;
                            }
                        }

                        if (!matchFound)
                        {
                            // Create a new group for this product
                            product.GroupId = Guid.NewGuid();
                            await repository.UpdateProductAsync(product);
                            _logger.LogInformation($"Created new group {product.GroupId} for {product.Name}");
                        }
                    }
                }

                await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); // Run every 5 minutes
            }
        }
    }
}
