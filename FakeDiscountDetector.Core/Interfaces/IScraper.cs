using System.Collections.Generic;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Core.Interfaces
{
    public interface IScraper
    {
        Task<List<Product>> ScrapeAsync();
    }
}
