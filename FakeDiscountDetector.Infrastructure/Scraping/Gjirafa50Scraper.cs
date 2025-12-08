using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;
using PuppeteerSharp;

namespace FakeDiscountDetector.Infrastructure.Scraping
{
    public class Gjirafa50Scraper : IScraper
    {
        private const string BaseUrl = "https://gjirafa50.com";

        public Gjirafa50Scraper()
        {
        }

        public async Task<List<Product>> ScrapeAsync()
        {
            var products = new List<Product>();

           
            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            };

            using var browser = await Puppeteer.LaunchAsync(launchOptions);
            using var page = await browser.NewPageAsync();

            
            await page.SetRequestInterceptionAsync(true);
            page.Request += (sender, e) =>
            {
                if (e.Request.ResourceType == ResourceType.Image ||
                    e.Request.ResourceType == ResourceType.Font)
                {
                    e.Request.AbortAsync();
                }
                else
                {
                    e.Request.ContinueAsync();
                }
            };

            try
            {
                // Go to homepage
                var url = BaseUrl;
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");
                await page.GoToAsync(url, WaitUntilNavigation.Networkidle2);

                
                await page.WaitForSelectorAsync(".product-item:not(.skeleton)", new WaitForSelectorOptions { Timeout = 15000 });

              
                int maxClicks = 20;
                for (int i = 0; i < maxClicks; i++)
                {
                    try
                    {
                        var loadMoreBtn = await page.QuerySelectorAsync("button.load-more-products-btn");
                        if (loadMoreBtn != null)
                        {
                            Console.WriteLine($"Clicking 'Load More' (attempt {i + 1}/{maxClicks})...");
                            await loadMoreBtn.ClickAsync();
                            // Wait for network activity to settle or a short delay
                            await Task.Delay(3000);
                        }
                        else
                        {
                            Console.WriteLine("No 'Load More' button found. Stopping pagination.");
                            break;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error clicking 'Load More': {ex.Message}");
                        break;
                    }
                }

                // Extract data
                var json = await page.EvaluateFunctionAsync<string>(@"() => {
                    const items = Array.from(document.querySelectorAll('.product-item:not(.skeleton)'));
                    if (items.length === 0) return '[]';
                    
                    const data = items.map(item => {
                        const nameEl = item.querySelector('.product-title a');
                        const priceEl = item.querySelector('.price.font-bold');
                        const oldPriceEl = item.querySelector('.price.old-price');
                        const imgEl = item.querySelector('.picture img');

                        return {
                            Name: nameEl ? nameEl.textContent.trim() : '',
                            Url: nameEl ? nameEl.getAttribute('href') : '',
                            Price: priceEl ? priceEl.textContent.trim() : '',
                            OldPrice: oldPriceEl ? oldPriceEl.textContent.trim() : '',
                            ImageUrl: imgEl ? imgEl.getAttribute('src') : ''
                        };
                    });
                    return JSON.stringify(data);
                }");

                var productData = System.Text.Json.JsonSerializer.Deserialize<List<ProductDto>>(json);

                foreach (var dto in productData)
                {
                    if (string.IsNullOrEmpty(dto.Name) || string.IsNullOrEmpty(dto.Price)) continue;

                    var priceString = dto.Price.Replace("€", "").Replace(",", "").Trim();
                    if (!decimal.TryParse(priceString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var price)) continue;

                    decimal? originalPrice = null;
                    if (!string.IsNullOrEmpty(dto.OldPrice))
                    {
                        var oldPriceString = dto.OldPrice.Replace("€", "").Replace(",", "").Trim();
                        if (decimal.TryParse(oldPriceString, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var op))
                        {
                            originalPrice = op;
                        }
                    }

                    var productUrl = dto.Url ?? "";
                    if (!string.IsNullOrEmpty(productUrl) && !productUrl.StartsWith("http"))
                    {
                        productUrl = BaseUrl + (productUrl.StartsWith("/") ? "" : "/") + productUrl;
                    }

                    products.Add(new Product
                    {
                        Name = dto.Name,
                        Url = productUrl,
                        StoreName = "Gjirafa50",
                        ImageUrl = dto.ImageUrl ?? "",
                        PriceHistory = new List<PriceRecord>
                        {
                            new PriceRecord
                            {
                                Price = price,
                                OriginalPrice = originalPrice,
                                Timestamp = DateTime.Now
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scraping Gjirafa50: {ex.Message}");
            }

            return products;
        }

        public class ProductDto
        {
            public string? Name { get; set; }
            public string? Url { get; set; }
            public string? Price { get; set; }
            public string? OldPrice { get; set; }
            public string? ImageUrl { get; set; }
        }
    }
}
