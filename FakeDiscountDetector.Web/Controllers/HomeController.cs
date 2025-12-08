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

    public async Task<IActionResult> Index(string? searchString, int? pageNumber)
    {
        int pageSize = 36;
        int pageIndex = pageNumber ?? 1;

        var products = await _repository.GetProductsAsync(searchString, pageIndex, pageSize);
        var totalCount = await _repository.GetTotalCountAsync(searchString);

        var paginatedList = new PaginatedList<Product>(products, totalCount, pageIndex, pageSize);

        var fakeDiscounts = new List<Product>();
        foreach (var product in products)
        {
            var latestPrice = product.PriceHistory.OrderByDescending(p => p.Timestamp).FirstOrDefault();
            if (latestPrice != null && _analyzer.IsFakeDiscount(product, latestPrice.Price, latestPrice.OriginalPrice))
            {
                fakeDiscounts.Add(product);
            }
        }

       
        foreach (var product in products)
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

      
        var biggestDiscounts = products
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
                DiscountAmount = x.LatestPrice.OriginalPrice - x.LatestPrice.Price
            })
            .OrderByDescending(x => x.DiscountAmount)
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
