using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;
using PuppeteerSharp;

namespace FakeDiscountDetector.Infrastructure.Scraping
{
    public class FolejaScraper : IScraper
    {
        private const string BaseUrl = "https://www.foleja.com/Foleja-e-teknologjise/";

        public FolejaScraper()
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
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");

                int pageNumber = 1;
                bool hasMoreProducts = true;

                while (hasMoreProducts)
                {
                    var url = $"{BaseUrl}?order=acris-score-desc&p={pageNumber}";
                    Console.WriteLine($"Scraping Foleja page {pageNumber}...");

                    await page.GoToAsync(url, WaitUntilNavigation.Networkidle2);

                    // Wait for product items to load
                    try
                    {
                        await page.WaitForSelectorAsync(".product-box", new WaitForSelectorOptions { Timeout = 10000 });
                    }
                    catch
                    {
                        Console.WriteLine($"No products found on page {pageNumber}. Stopping.");
                        break;
                    }

                    // Extract data
                    var json = await page.EvaluateFunctionAsync<string>(@"() => {
                        const items = Array.from(document.querySelectorAll('.product-box'));
                        if (items.length === 0) return '[]';
                        
                        const data = items.map(item => {
                            const nameEl = item.querySelector('.product-name');
                            const wholePriceEl = item.querySelector('.whole-original-price');
                            const decimalPriceEl = item.querySelector('.decimal-rounded-price');
                            const oldPriceEl = item.querySelector('.list-price-price');
                            const imgEl = item.querySelector('.product-image-link img');
                            const linkEl = item.querySelector('.product-image-link');

                            let price = '';
                            if (wholePriceEl) {
                                // Text content is like '€ 159 00'
                                let text = wholePriceEl.textContent.trim().replace('€', '').trim();
                                
                                let dec = '';
                                if (decimalPriceEl) {
                                    dec = decimalPriceEl.textContent.trim();
                                    if (text.endsWith(dec)) {
                                        text = text.substring(0, text.length - dec.length).trim();
                                    }
                                }
                                
                                // Clean up whole part
                                text = text.replace(/[^\d]/g, '');
                                
                                price = text + '.' + dec;
                            }

                            return {
                                Name: nameEl ? nameEl.textContent.trim() : '',
                                Url: linkEl ? linkEl.getAttribute('href') : '',
                                Price: price,
                                OldPrice: oldPriceEl ? oldPriceEl.textContent.trim() : '',
                                ImageUrl: imgEl ? imgEl.getAttribute('src') : ''
                            };
                        });
                        return JSON.stringify(data);
                    }");

                    var productData = System.Text.Json.JsonSerializer.Deserialize<List<ProductDto>>(json);

                    if (productData == null || productData.Count == 0)
                    {
                        hasMoreProducts = false;
                        break;
                    }

                    foreach (var dto in productData)
                    {
                        if (string.IsNullOrEmpty(dto.Name) || string.IsNullOrEmpty(dto.Price)) continue;

                        // Remove '€' and whitespace
                        var priceClean = dto.Price.Replace("€", "").Trim();



                        var priceString = priceClean.Replace(".", "").Replace(",", ""); 
                                                                                       

                        if (!decimal.TryParse(priceString, out var rawPrice)) continue;


                        var parts = priceClean.Split('.');
                        decimal price = 0;

                        if (parts.Length > 1)
                        {
                         
                            var decimalPart = parts[parts.Length - 1];
                            var wholePart = string.Join("", parts, 0, parts.Length - 1); 

                            if (decimal.TryParse($"{wholePart}.{decimalPart}", System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var p))
                            {
                                price = p;
                            }
                        }
                        else
                        {

                            if (decimal.TryParse(priceClean.Replace(".", ""), out var p))
                            {
                                price = p;
                            }
                        }

                        if (price == 0) continue;

                        decimal? originalPrice = null;
                        if (!string.IsNullOrEmpty(dto.OldPrice))
                        {

                            var opStr = dto.OldPrice.Replace("€", "").Trim();

                            opStr = opStr.Replace(".", "").Replace(",", ".");

                            if (decimal.TryParse(opStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var op))
                            {
                                originalPrice = op;
                            }
                        }

                        var productUrl = dto.Url ?? "";
                        if (!string.IsNullOrEmpty(productUrl) && !productUrl.StartsWith("http"))
                        {
                            productUrl = "https://www.foleja.com" + (productUrl.StartsWith("/") ? "" : "/") + productUrl;
                        }

                        products.Add(new Product
                        {
                            Name = dto.Name,
                            Url = productUrl,
                            StoreName = "Foleja",
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

                    // Limit pages for now to avoid scraping too much
                    if (pageNumber >= 20) hasMoreProducts = false;
                    pageNumber++;

                    // Small delay between pages
                    await Task.Delay(2000);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error scraping Foleja: {ex.Message}");
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
