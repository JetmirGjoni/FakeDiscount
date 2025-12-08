using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Infrastructure.Data;
using FakeDiscountDetector.Infrastructure.Services;
using FakeDiscountDetector.Infrastructure.AI;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);


builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("Data Source=fakediscount.db"));

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IDiscountAnalyzer, DiscountAnalyzer>();
builder.Services.AddScoped<IPricePredictor, SimplePricePredictor>();
builder.Services.AddScoped<IProductMatcher, TokenBasedProductMatcher>();


builder.Services.AddHttpClient();
builder.Services.AddScoped<MLProductClassifier>();
builder.Services.AddScoped<GeminiFallbackService>();
builder.Services.AddScoped<ITrainingService, MLTrainingService>();
builder.Services.AddScoped<IProductClassifier, HybridClassifier>();

var app = builder.Build();


if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure DB is created
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
    DataSeeder.SeedAsync(db).Wait();

    // trajnimi modelit 
    var trainingService = scope.ServiceProvider.GetRequiredService<ITrainingService>() as MLTrainingService;
    if (trainingService != null)
    {
        var csvPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "training_data.csv");
        var entries = await trainingService.LoadFromCsvAsync(csvPath);
        if (entries.Any())
        {
            Console.WriteLine($"Loading {entries.Count()} training examples from CSV...");
            // Convert ProductEntry to Product for training
            var products = entries.Select(e => new Product
            {
                Name = e.Name,
                StoreName = e.StoreName,
                Category = e.Category,
                PriceHistory = new List<PriceRecord>
                {
                    new PriceRecord { Price = (decimal)e.Price, Timestamp = DateTime.Now }
                }
            });
            await trainingService.TrainModelAsync(products);
            Console.WriteLine("ML model trained successfully!");
        }
    }
}

app.Run();
