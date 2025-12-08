using System.Collections.Generic;
using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Web.ViewModels
{
    public class ProductDetailsViewModel
    {
        public Product Product { get; set; } = null!;
        public PricePrediction? Prediction { get; set; }
        public List<Product> Competitors { get; set; } = new List<Product>();
    }
}
