using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Core.Configurations;
using PuppeteerSharp;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace FakeDiscountDetector.Infrastructure.Scraping
{
    public class GenericConfigurableScraper : IScraper
    {
        private readonly ScraperConfig _config;

        public GenericConfigurableScraper(ScraperConfig config)
        {
            _config = config;
        }

        public async Task<List<string>> DiscoverCategoriesAsync()
        {
            if (string.IsNullOrEmpty(_config.CategorySelector)) return new List<string>();

            var browserFetcher = new BrowserFetcher();
            await browserFetcher.DownloadAsync();

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            };

            using var browser = await Puppeteer.LaunchAsync(launchOptions);
            using var page = await browser.NewPageAsync();

            try
            {
                Console.WriteLine($"[{_config.Name}] Discovering categories from {_config.BaseUrl}");
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");
                await page.GoToAsync(_config.BaseUrl, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }, Timeout = 60000 });

                await Task.Delay(3000);

                var links = await page.EvaluateFunctionAsync<string[]>(@"(selector) => {
                    const elements = Array.from(document.querySelectorAll(selector));
                    return elements
                        .map(el => el.href)
                        .filter(href => href && href.startsWith('http'));
                }", _config.CategorySelector);

                var uniqueLinks = links.Distinct().ToList();
                Console.WriteLine($"[{_config.Name}] Discovered {uniqueLinks.Count} unique categories.");
                return uniqueLinks;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_config.Name}] Error discovering categories: {ex.Message}");
                return new List<string>();
            }
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
            page.Console += (sender, e) =>
            {
                Console.WriteLine($"[Browser Console] {e.Message.Text}");
            };

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
                var url = _config.TargetUrl ?? _config.BaseUrl;
                Console.WriteLine($"[{_config.Name}] Starting scrape of {url}");
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/114.0.0.0 Safari/537.36");
                await page.GoToAsync(url, new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }, Timeout = 60000 });

                await Task.Delay(5000); // Increased buffer

                if (_config.PaginationType == "LoadMoreButton" && !string.IsNullOrEmpty(_config.PaginationSelector))
                {
                    int maxClicks = _config.MaxPages;
                    for (int i = 0; i < maxClicks; i++)
                    {
                        try
                        {
                            var loadMoreBtn = await page.QuerySelectorAsync(_config.PaginationSelector);
                            if (loadMoreBtn != null)
                            {
                                Console.WriteLine($"[{_config.Name}] Clicking 'Load More' (attempt {i + 1}/{maxClicks})...");
                                await loadMoreBtn.ClickAsync();
                                await Task.Delay(4000);
                            }
                            else break;
                        }
                        catch { break; }
                    }
                    await ExtractPageProducts(page, products);
                }
                else if (_config.PaginationType == "NextPageLink" && !string.IsNullOrEmpty(_config.PaginationSelector))
                {
                    int maxPages = _config.MaxPages;
                    for (int p = 1; p <= maxPages; p++)
                    {
                        Console.WriteLine($"[{_config.Name}] Scraping page {p}...");
                        await ExtractPageProducts(page, products);

                        if (p < maxPages)
                        {
                            try
                            {
                                var nextBtn = await page.QuerySelectorAsync(_config.PaginationSelector);
                                if (nextBtn != null)
                                {
                                    Console.WriteLine($"[{_config.Name}] Navigating to next page...");
                                    await Task.WhenAll(
                                        nextBtn.ClickAsync(),
                                        page.WaitForNavigationAsync(new NavigationOptions { WaitUntil = new[] { WaitUntilNavigation.DOMContentLoaded }, Timeout = 60000 })
                                    );
                                    await Task.Delay(3000);
                                }
                                else break;
                            }
                            catch { break; }
                        }
                    }
                }
                else
                {
                    await ExtractPageProducts(page, products);
                }

                Console.WriteLine($"[{_config.Name}] Scraping completed. Total products extracted: {products.Count}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_config.Name}] Error scraping: {ex.Message}");
                try
                {
                    await page.ScreenshotAsync($"/Users/src/Desktop/FakeDiscount/debug_{_config.Name}.png");
                }
                catch { }
            }

            return products;
        }

        private async Task ExtractPageProducts(IPage page, List<Product> products)
        {
            try
            {
                Console.WriteLine($"[{_config.Name}] Waiting for items with selector: {_config.ItemSelector}");
                await page.WaitForSelectorAsync(_config.ItemSelector, new WaitForSelectorOptions { Timeout = 30000 });

                var json = await page.EvaluateFunctionAsync<string>(@"(config) => {
                    const getItem = (obj, key) => obj[key] || obj[key.charAt(0).toLowerCase() + key.slice(1)];
                    const itemSelector = getItem(config, 'ItemSelector');
                    const nameSelector = getItem(config, 'NameSelector');
                    const priceSelector = getItem(config, 'PriceSelector');
                    const oldPriceSelector = getItem(config, 'OldPriceSelector');
                    const imageSelector = getItem(config, 'ImageSelector');

                    const items = Array.from(document.querySelectorAll(itemSelector));
                    console.log(`[Browser] Found ${items.length} items with selector: ${itemSelector}`);
                    
                    const data = items.map((item, index) => {
                        const nameEl = item.querySelector(nameSelector);
                        const priceEl = item.querySelector(priceSelector);
                        const oldPriceEl = oldPriceSelector ? item.querySelector(oldPriceSelector) : null;
                        const imgEl = imageSelector ? item.querySelector(imageSelector) : null;

                        if (!nameEl || !priceEl) {
                             if (index < 2) {
                                 console.log(`[Browser] Item ${index} missing Name or Price. NameSel: ${nameSelector}, PriceSel: ${priceSelector}`);
                             }
                             return null;
                        }

                        return {
                            Name: nameEl.textContent.trim(),
                            Url: nameEl.tagName === 'A' ? nameEl.href : (item.querySelector('a')?.href || window.location.href),
                            Price: priceEl.textContent.trim(),
                            OldPrice: oldPriceEl ? oldPriceEl.textContent.trim() : '',
                            ImageUrl: imgEl ? (imgEl.tagName === 'IMG' ? imgEl.src : imgEl.getAttribute('data-src') || imgEl.src) : ''
                        };
                    }).filter(x => x !== null);
                    return JSON.stringify(data);
                }", _config);

                if (string.IsNullOrEmpty(json)) return;

                var productData = JsonSerializer.Deserialize<List<ProductDto>>(json);
                if (productData != null)
                {
                    int addedCount = 0;
                    foreach (var dto in productData)
                    {
                        var product = MapToProduct(dto);
                        if (product != null && !products.Any(p => p.Url == product.Url))
                        {
                            products.Add(product);
                            addedCount++;
                        }
                    }
                    Console.WriteLine($"[{_config.Name}] Successfully mapped {addedCount} new products from this page.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{_config.Name}] Extraction error: {ex.Message}");
            }
        }

        private Product MapToProduct(ProductDto dto)
        {
            if (string.IsNullOrEmpty(dto.Name) || string.IsNullOrEmpty(dto.Price)) return null;

            var price = ParsePrice(dto.Price);
            if (price == null) return null;
            price *= _config.PriceMultiplier;

            decimal? originalPrice = null;
            if (!string.IsNullOrEmpty(dto.OldPrice))
            {
                var op = ParsePrice(dto.OldPrice);
                if (op.HasValue)
                    originalPrice = op.Value * _config.OldPriceMultiplier;
            }

            var productUrl = dto.Url ?? "";
            if (!string.IsNullOrEmpty(productUrl) && !productUrl.StartsWith("http"))
            {
                var uri = new Uri(_config.TargetUrl ?? _config.BaseUrl);
                var baseDomain = $"{uri.Scheme}://{uri.Host}";
                productUrl = baseDomain + (productUrl.StartsWith("/") ? "" : "/") + productUrl;
            }

            return new Product
            {
                Name = dto.Name,
                Url = productUrl,
                ImageUrl = dto.ImageUrl,
                StoreName = _config.Name,
                Source = _config.TargetUrl ?? _config.BaseUrl,
                PriceHistory = new List<PriceRecord>
                {
                    new PriceRecord
                    {
                        Price = price.Value,
                        OriginalPrice = originalPrice,
                        Timestamp = DateTime.UtcNow
                    }
                }
            };
        }

        private decimal? ParsePrice(string input)
        {
            if (string.IsNullOrEmpty(input)) return null;

            // Remove non-numeric chars except delimiters
            var cleaned = new string(input.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-').ToArray());
            if (string.IsNullOrEmpty(cleaned)) return null;

            // Use configured culture if available
            if (!string.IsNullOrEmpty(_config.PriceCulture))
            {
                try
                {
                    var culture = System.Globalization.CultureInfo.GetCultureInfo(_config.PriceCulture);
                    if (decimal.TryParse(cleaned, System.Globalization.NumberStyles.Any, culture, out var result))
                    {
                        return result;
                    }
                    Console.WriteLine($"[{_config.Name}] Failed to parse '{cleaned}' with culture '{_config.PriceCulture}'");
                    return null;
                }
                catch (System.Globalization.CultureNotFoundException)
                {
                    Console.WriteLine($"[{_config.Name}] Invalid culture '{_config.PriceCulture}'. Falling back to heuristics.");
                }
            }

            // Fallback to heuristics (existing logic)
            int lastDot = cleaned.LastIndexOf('.');
            int lastComma = cleaned.LastIndexOf(',');

            string heuristicCleaned = cleaned;

            if (lastDot > lastComma) // Format looks like 1.23 or 1,234.56 or 1.234
            {
                // Heuristic: If exactly 3 digits follow the dot, it's likely a thousands separator (e.g. 2.699)
                if (cleaned.Length - lastDot - 1 == 3)
                {
                    heuristicCleaned = cleaned.Replace(".", "").Replace(",", "");
                }
                else
                {
                    heuristicCleaned = cleaned.Replace(",", "");
                }
            }
            else if (lastComma > lastDot) // Format looks like 1,23 or 1.234,56 or 1,234
            {
                // Heuristic: If exactly 3 digits follow the comma, it's likely a thousands separator (e.g. 1,234)
                if (cleaned.Length - lastComma - 1 == 3)
                {
                    heuristicCleaned = cleaned.Replace(",", "").Replace(".", "");
                }
                else
                {
                    heuristicCleaned = cleaned.Replace(".", "").Replace(",", ".");
                }
            }

            if (decimal.TryParse(heuristicCleaned, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var finalPrice))
            {
                return finalPrice;
            }

            return null;
        }

        private class ProductDto
        {
            public string? Name { get; set; }
            public string? Url { get; set; }
            public string? Price { get; set; }
            public string? OldPrice { get; set; }
            public string? ImageUrl { get; set; }
        }
    }
}
