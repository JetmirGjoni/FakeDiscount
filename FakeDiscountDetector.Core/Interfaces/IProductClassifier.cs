using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Core.Interfaces
{
    public interface IProductClassifier
    {
        Task<string> PredictCategoryAsync(Product product);
        Task<(string Category, float Confidence)> PredictCategoryWithConfidenceAsync(Product product);
    }
}
