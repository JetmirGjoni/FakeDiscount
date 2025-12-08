using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Core.Interfaces
{
    public interface IPricePredictor
    {
        Task<PricePrediction> PredictNextPriceAsync(Product product);
    }
}
