using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Core.Interfaces
{
    public interface IDiscountAnalyzer
    {
        bool IsFakeDiscount(Product product, decimal currentPrice, decimal? originalPrice);
        decimal CalculateDiscountPercentage(decimal currentPrice, decimal originalPrice);
    }
}
