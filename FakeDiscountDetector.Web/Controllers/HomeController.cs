using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using FakeDiscountDetector.Web.Models;
using FakeDiscountDetector.Web.ViewModels;
using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IProductRepository _repository;
    private readonly IDiscountAnalyzer _analyzer;
    private readonly IProductClassifier _classifier;

    public HomeController(ILogger<HomeController> logger, IProductRepository repository, IDiscountAnalyzer analyzer, IProductClassifier classifier)
    {
        _logger = logger;
        _repository = repository;
        _analyzer = analyzer;
        _classifier = classifier;
    }

    public async Task<IActionResult> Index(string? searchString, string? category, int? pageNumber)
    {
        int pageSize = 36;
        int pageIndex = pageNumber ?? 1;

        // Defined category list per user requirement
        var allCategories = new List<string> {
            "Smartphone", "Audio", "Laptop", "Tablet", "Smartwatch", "Monitor", "TV",
            "Computer Accessories", "Networking", "Gaming Console", "Gaming Accessories",
            "Camera", "Smart Home", "Storage", "Other"
        };

        // Defined store list per user requirement (from scrapers.json)
        var allStores = new List<string> {
            "AmazonDE", "AztechOnline", "ButonKS", "eBaa", "Foleja", "Gjirafa50",
            "GjirafaMall", "NeptunKS", "ShopAz", "TopShopKS"
        };

        // We sort them for display
        allCategories.Sort();
        allStores.Sort();

        ViewBag.Categories = allCategories;
        ViewBag.Stores = allStores;
        ViewBag.CurrentCategory = category;

       

        var products = await _repository.GetProductsAsync(searchString, 1, 10000); 

        if (!string.IsNullOrEmpty(category))
        {
            products = products.Where(p => p.Category == category).ToList();
        }

        var totalCount = products.Count;

     
        var pagedProducts = products
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var paginatedList = new PaginatedList<Product>(pagedProducts, totalCount, pageIndex, pageSize);

        var fakeDiscounts = new List<Product>();
        foreach (var product in pagedProducts)
        {
            var latestPrice = product.PriceHistory.OrderByDescending(p => p.Timestamp).FirstOrDefault();
            if (latestPrice != null && _analyzer.IsFakeDiscount(product, latestPrice.Price, latestPrice.OriginalPrice))
            {
                fakeDiscounts.Add(product);
            }
        }

       
        foreach (var product in pagedProducts)
        {
            if (string.IsNullOrEmpty(product.Category) || product.Category == "Uncategorized")
            {
                try
                {
                    product.Category = await _classifier.PredictCategoryAsync(product);
                    await _repository.UpdateProductAsync(product);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error classifying product {product.Name}");
                    product.Category = "Uncategorized";
                }
            }
        }

        ViewBag.FakeDiscounts = fakeDiscounts;
        ViewData["CurrentFilter"] = searchString;

    
        var biggestDiscounts = pagedProducts
            .Where(p => !fakeDiscounts.Contains(p))
            .Select(p => new
            {
                Product = p,
                LatestPrice = p.PriceHistory.OrderByDescending(ph => ph.Timestamp).FirstOrDefault()
            })
            .Where(x => x.LatestPrice != null && x.LatestPrice.OriginalPrice.HasValue && x.LatestPrice.OriginalPrice > x.LatestPrice.Price)
            .Select(x => new
            {
                x.Product,
                x.LatestPrice,
                DiscountPercentage = (x.LatestPrice.OriginalPrice.Value - x.LatestPrice.Price) / x.LatestPrice.OriginalPrice.Value
            })
            .OrderByDescending(x => x.DiscountPercentage)
            .Take(5)
            .Select(x => x.Product)
            .ToList();

        ViewBag.BiggestDiscounts = biggestDiscounts;

        return View(paginatedList);
    }

    [HttpGet]
    public async Task<IActionResult> SearchSuggestions(string term)
    {
        if (string.IsNullOrWhiteSpace(term) || term.Length < 3)
        {
            return Json(new List<object>());
        }

        var products = await _repository.GetProductsAsync(term, 1, 5);
        var suggestions = products.Select(p => new
        {
            id = p.Id,
            label = p.Name,
            price = p.PriceHistory.OrderByDescending(ph => ph.Timestamp).FirstOrDefault()?.Price,
            imageUrl = p.ImageUrl
        });

        return Json(suggestions);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
