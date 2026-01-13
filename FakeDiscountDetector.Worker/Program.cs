using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Infrastructure.Data;
using FakeDiscountDetector.Infrastructure.Scraping;
using FakeDiscountDetector.Infrastructure.Services;
using FakeDiscountDetector.Infrastructure.AI;
using FakeDiscountDetector.Infrastructure.Messaging;
using FakeDiscountDetector.Worker;
using Microsoft.EntityFrameworkCore;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        var connectionString = hostContext.Configuration.GetConnectionString("DefaultConnection")
                               ?? "Data Source=fakediscount.db";

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(connectionString));


        if (args.Contains("--use-rabbitmq"))
        {
            Console.WriteLine("Running with RabbitMQ");
            services.AddSingleton<FakeDiscountDetector.Infrastructure.Messaging.RabbitMQService>();
            services.AddSingleton<IMessageQueueService>(sp => sp.GetRequiredService<FakeDiscountDetector.Infrastructure.Messaging.RabbitMQService>());
        }
        else
        {
            Console.WriteLine("Running in LOCAL MODE (In-Memory Queue)");
            services.AddSingleton<IMessageQueueService, InMemoryMessageQueueService>();
        }

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
        services.AddHostedService<SchedulingWorker>();  // Publisher
        // services.AddHostedService<ProductMatchingWorker>(); // Can keep or remove depending on verifying scraping only
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
