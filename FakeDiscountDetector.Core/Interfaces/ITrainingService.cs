using System.Collections.Generic;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Core.Interfaces
{
    public interface ITrainingService
    {
        Task TrainModelAsync(IEnumerable<Product> trainingData);
        Task AddTrainingExampleAsync(Product product, string category);
    }
}
