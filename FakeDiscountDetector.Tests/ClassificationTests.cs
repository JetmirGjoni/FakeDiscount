using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Infrastructure.AI;
using Xunit;

namespace FakeDiscountDetector.Tests
{
    public class ClassificationTests
    {
        [Fact]
        public async Task MLTrainingService_LoadFromCsv_ParsesDataCorrectly()
        {
        
            var service = new MLTrainingService();
            var csvPath = "../../../test_data.csv";
            
        
            var testData = @"Name,StoreName,Price,Category
""Sony WH-1000XM5 Wireless Headphones"",Gjirafa50,100.0,Audio
""Samsung Galaxy S23 Ultra"",Gjirafa50,100.0,Smartphone
""Logitech MX Master 3S"",Gjirafa50,50.0,Other";
            
            System.IO.File.WriteAllText(csvPath, testData);

            
            var entries = await service.LoadFromCsvAsync(csvPath);
            var list = entries.ToList();

      
            Assert.Equal(3, list.Count);
            Assert.Equal("Sony WH-1000XM5 Wireless Headphones", list[0].Name);
            Assert.Equal("Audio", list[0].Category);
            Assert.Equal(100.0f, list[0].Price);

        
            System.IO.File.Delete(csvPath);
        }

        [Fact]
        public async Task MLTrainingService_TrainModel_CreatesModelFile()
        {
            // Arrange
            var service = new MLTrainingService();
            var products = new List<Product>
            {
                new Product
                {
                    Name = "iPhone 15 Pro",
                    StoreName = "Gjirafa50",
                    Category = "Smartphone",
                    PriceHistory = new List<PriceRecord>
                    {
                        new PriceRecord { Price = 999, Timestamp = System.DateTime.Now }
                    }
                },
                new Product
                {
                    Name = "MacBook Pro",
                    StoreName = "Gjirafa50",
                    Category = "Laptop",
                    PriceHistory = new List<PriceRecord>
                    {
                        new PriceRecord { Price = 1999, Timestamp = System.DateTime.Now }
                    }
                }
            };

         
            await service.TrainModelAsync(products);

           
            var modelPath = System.IO.Path.Combine(System.AppContext.BaseDirectory, "model.zip");
            Assert.True(System.IO.File.Exists(modelPath), "Model file should be created");
        }

        [Fact]
        public async Task MLProductClassifier_PredictCategory_ReturnsValidCategory()
        {
            // Arrange
            var trainingService = new MLTrainingService();
            var products = new List<Product>
            {
                new Product { Name = "iPhone 15", StoreName = "Gjirafa50", Category = "Smartphone", 
                    PriceHistory = new List<PriceRecord> { new PriceRecord { Price = 999 } } },
                new Product { Name = "Samsung Galaxy S24", StoreName = "Gjirafa50", Category = "Smartphone", 
                    PriceHistory = new List<PriceRecord> { new PriceRecord { Price = 899 } } },
                new Product { Name = "MacBook Pro", StoreName = "Gjirafa50", Category = "Laptop", 
                    PriceHistory = new List<PriceRecord> { new PriceRecord { Price = 1999 } } },
                new Product { Name = "Dell XPS", StoreName = "Gjirafa50", Category = "Laptop", 
                    PriceHistory = new List<PriceRecord> { new PriceRecord { Price = 1499 } } }
            };

            await trainingService.TrainModelAsync(products);

            var classifier = new MLProductClassifier();

        
            var testProduct = new Product
            {
                Name = "Google Pixel 8",
                StoreName = "Gjirafa50",
                PriceHistory = new List<PriceRecord> { new PriceRecord { Price = 699 } }
            };

            var category = await classifier.PredictCategoryAsync(testProduct);

            
            Assert.NotNull(category);
            Assert.NotEmpty(category);
           
            Assert.Contains("Smartphone", category, System.StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [InlineData("Sony WH-1000XM5 Headphones", "Audio")]
        [InlineData("Apple iPhone 17 Pro Max", "Smartphone")]
        [InlineData("Lenovo ThinkPad", "Laptop")]
        [InlineData("Samsung QLED TV", "TV")]
        public async Task Classification_SampleProducts_GetCorrectCategories(string productName, string expectedCategory)
        {
    
            var trainingService = new MLTrainingService();
            var csvPath = "../../../../training_data.csv";

            // Load training data from actual CSV
            var entries = await trainingService.LoadFromCsvAsync(csvPath);
            if (entries.Any())
            {
                var products = entries.Select(e => new Product
                {
                    Name = e.Name,
                    StoreName = e.StoreName,
                    Category = e.Category,
                    PriceHistory = new List<PriceRecord>
                    {
                        new PriceRecord { Price = (decimal)e.Price, Timestamp = System.DateTime.Now }
                    }
                }).ToList();

                await trainingService.TrainModelAsync(products);
            }

            var classifier = new MLProductClassifier();

          
            var testProduct = new Product
            {
                Name = productName,
                StoreName = "Gjirafa50",
                PriceHistory = new List<PriceRecord> { new PriceRecord { Price = 100 } }
            };

            var category = await classifier.PredictCategoryAsync(testProduct);

         
            Assert.NotNull(category);
            Assert.Contains(expectedCategory, category, System.StringComparison.OrdinalIgnoreCase);
        }
    }
}
