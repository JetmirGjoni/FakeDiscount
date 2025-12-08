using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Core.Interfaces
{
    public interface IProductMatcher
    {
     
        double CalculateSimilarity(Product productA, Product productB);


        bool AreMatch(Product productA, Product productB, double threshold = 0.8);
    }
}
