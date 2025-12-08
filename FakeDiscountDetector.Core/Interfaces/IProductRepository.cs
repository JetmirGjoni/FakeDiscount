using System.Collections.Generic;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;

namespace FakeDiscountDetector.Core.Interfaces
{
    public interface IProductRepository
    {
        Task<Product?> GetProductByUrlAsync(string url);
        Task AddProductAsync(Product product);
        Task UpdateProductAsync(Product product);
        Task AddPriceRecordAsync(PriceRecord priceRecord);
        Task<List<Product>> GetAllProductsAsync();
        Task<Product?> GetProductByIdAsync(int id);
        Task<List<Product>> GetUngroupedProductsAsync();

        // Search ,Pagination
        Task<List<Product>> GetProductsAsync(string? searchTerm, int pageIndex, int pageSize);
        Task<int> GetTotalCountAsync(string? searchTerm);
    }
}
