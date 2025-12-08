using System;
using System.Linq;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;

namespace FakeDiscountDetector.Infrastructure.Services
{
    public class DiscountAnalyzer : IDiscountAnalyzer
    {
        public decimal CalculateDiscountPercentage(decimal currentPrice, decimal originalPrice)
        {
            if (originalPrice == 0) return 0;
            return ((originalPrice - currentPrice) / originalPrice) * 100;
        }

        public bool IsFakeDiscount(Product product, decimal currentPrice, decimal? originalPrice)
        {


            if (originalPrice == null || originalPrice <= currentPrice) return false; // Not a discount or invalid

            if (product.PriceHistory == null || !product.PriceHistory.Any())
            {
          
                return false;
            }



            var recentHistory = product.PriceHistory.OrderByDescending(p => p.Timestamp).Take(10).ToList();


            if (recentHistory.Count >= 5)
            {
                var avgPrice = recentHistory.Average(p => p.Price);
                if (originalPrice.Value > avgPrice * 1.2m) 
                {
                    return true; 
                }
            }

            return false;
        }
    }
}
