using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Infrastructure.Data;
using FakeDiscountDetector.Infrastructure.Scraping;
using FakeDiscountDetector.Infrastructure.Services;
using FakeDiscountDetector.Infrastructure.AI;
using FakeDiscountDetector.Worker;
using Microsoft.EntityFrameworkCore;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite("Data Source=../FakeDiscountDetector.Web/fakediscount.db")); // Shared DB with Web

        services.AddScoped<IScraper, Gjirafa50Scraper>();
        services.AddScoped<IScraper, FolejaScraper>();
        // more scrapers.....
        // services.AddHttpClient<IScraper, AmazonScraper>();
        services.AddScoped<IProductRepository, ProductRepository>();
        services.AddScoped<IDiscountAnalyzer, DiscountAnalyzer>();
        services.AddScoped<IProductMatcher, TokenBasedProductMatcher>();

        // AI Services
        services.AddHttpClient();
        services.AddScoped<MLProductClassifier>();
        services.AddScoped<GeminiFallbackService>();
        services.AddScoped<ITrainingService, MLTrainingService>();
        services.AddScoped<IProductClassifier, HybridClassifier>();

        services.AddHostedService<ScrapingWorker>();
        services.AddHostedService<ProductMatchingWorker>();
    })
    .Build();

// Ensure DB is created
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}

// await host.RunAsync();

await host.RunAsync();
