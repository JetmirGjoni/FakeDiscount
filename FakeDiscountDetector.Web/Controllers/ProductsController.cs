using Microsoft.AspNetCore.Mvc;
using FakeDiscountDetector.Core.Interfaces;
using FakeDiscountDetector.Web.ViewModels;
using FakeDiscountDetector.Core.Entities;
using System.Linq;

namespace FakeDiscountDetector.Web.Controllers
{
    public class ProductsController : Controller
    {
        private readonly IProductRepository _repository;
        private readonly IPricePredictor _pricePredictor;
        private readonly IProductClassifier _classifier;

        public ProductsController(IProductRepository repository, IPricePredictor pricePredictor, IProductClassifier classifier)
        {
            _repository = repository;
            _pricePredictor = pricePredictor;
            _classifier = classifier;
        }

        [HttpPost]
        public async Task<IActionResult> Classify(int id)
        {
            var product = await _repository.GetProductByIdAsync(id);
            if (product == null) return NotFound();

            var category = await _classifier.PredictCategoryAsync(product);
            product.Category = category;


            await _repository.UpdateProductAsync(product);

            return RedirectToAction("Details", new { id = product.Id });
        }

        public async Task<IActionResult> Details(int id)
        {
            var product = await _repository.GetProductByIdAsync(id);
            if (product == null)
            {
                return NotFound();
            }

            var prediction = await _pricePredictor.PredictNextPriceAsync(product);

            var allProducts = await _repository.GetAllProductsAsync();
            var competitors = new List<Product>();

            if (product.GroupId != null)
            {
                competitors = allProducts
                    .Where(p => p.GroupId == product.GroupId && p.Id != product.Id)
                    .ToList();
            }

            var viewModel = new ProductDetailsViewModel
            {
                Product = product,
                Prediction = prediction,
                Competitors = competitors
            };

            return View(viewModel);
        }
    }
}
