using FakeDiscountDetector.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace FakeDiscountDetector.Infrastructure.Data
{//perdorur per testim te aplikationit ne fazat fillestre
    public static class DataSeeder
    {
        public static async Task SeedAsync(AppDbContext context)
        {
            if (await context.Products.AnyAsync())
            {
                return; // DB already seeded
            }

            var products = new List<Product>
            {

                new Product
                {
                    Name = "Sony WH-1000XM5 Wireless Headphones",
                    Url = "https://gjirafa50.com/sony-wh-1000xm5",
                    ImageUrl = "https://m.media-amazon.com/images/I/51SKmu2G9FL._AC_UF1000,1000_QL80_.jpg",
                    StoreName = "Gjirafa50",
                    PriceHistory = new List<PriceRecord>
                    {
                        new PriceRecord { Price = 100, Timestamp = DateTime.Now.AddDays(-10) },
                        new PriceRecord { Price = 100, Timestamp = DateTime.Now.AddDays(-5) },
                        new PriceRecord { Price = 80, OriginalPrice = 100, Timestamp = DateTime.Now }
                    }
                },


                new Product
                {
                    Name = "Samsung Galaxy S23 Ultra",
                    Url = "https://gjirafa50.com/samsung-s23-ultra",
                    ImageUrl = "https://images.samsung.com/is/image/samsung/p6pim/uk/2302/gallery/uk-galaxy-s23-ultra-s918-447009-sm-s918bzkpeub-534863401?$650_519_PNG$",
                    StoreName = "Gjirafa50",
                    PriceHistory = new List<PriceRecord>
                    {
                        new PriceRecord { Price = 100, Timestamp = DateTime.Now.AddDays(-20) },
                        new PriceRecord { Price = 100, Timestamp = DateTime.Now.AddDays(-10) },
                        new PriceRecord { Price = 100, Timestamp = DateTime.Now.AddDays(-2) },

                        new PriceRecord { Price = 110, OriginalPrice = 150, Timestamp = DateTime.Now }
                    }
                },


                new Product
                {
                    Name = "Logitech MX Master 3S",
                    Url = "https://gjirafa50.com/logitech-mx-master-3s",
                    ImageUrl = "https://resource.logitech.com/w_692,c_lpad,ar_4:3,q_auto,f_auto,dpr_1.0/d_transparent.gif/content/dam/logitech/en/products/mice/mx-master-3s/gallery/mx-master-3s-mouse-top-view-graphite.png?v=1",
                    StoreName = "Gjirafa50",
                    PriceHistory = new List<PriceRecord>
                    {
                        new PriceRecord { Price = 50, Timestamp = DateTime.Now.AddDays(-30) },
                        new PriceRecord { Price = 50, Timestamp = DateTime.Now.AddDays(-15) },
                        new PriceRecord { Price = 50, Timestamp = DateTime.Now }
                    }
                }
            };

            context.Products.AddRange(products);
            await context.SaveChangesAsync();
        }
    }
}
