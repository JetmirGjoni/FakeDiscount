using System.Collections.Generic;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FakeDiscountDetector.Infrastructure.Data
{
    public class ProductRepository : IProductRepository
    {
        private readonly AppDbContext _context;

        public ProductRepository(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> GetProductByUrlAsync(string url)
        {
            return await _context.Products
                .Include(p => p.PriceHistory)
                .FirstOrDefaultAsync(p => p.Url == url);
        }

        public async Task AddProductAsync(Product product)
        {
            await _context.Products.AddAsync(product);
            await _context.SaveChangesAsync();
        }

        public async Task UpdateProductAsync(Product product)
        {
            _context.Products.Update(product);
            await _context.SaveChangesAsync();
        }

        public async Task AddPriceRecordAsync(PriceRecord priceRecord)
        {
            await _context.PriceRecords.AddAsync(priceRecord);
            await _context.SaveChangesAsync();
        }

        public async Task<List<Product>> GetAllProductsAsync()
        {
            return await _context.Products
                .Include(p => p.PriceHistory)
                .ToListAsync();
        }

        public async Task<Product?> GetProductByIdAsync(int id)
        {
            return await _context.Products
                .Include(p => p.PriceHistory)
                .FirstOrDefaultAsync(p => p.Id == id);
        }

        public async Task<List<Product>> GetUngroupedProductsAsync()
        {
            return await _context.Products
                .Where(p => p.GroupId == null)
                .ToListAsync();
        }

        public async Task<List<Product>> GetProductsAsync(string? searchTerm, int pageIndex, int pageSize)
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Name.ToLower().Contains(searchTerm.ToLower()));
            }

            return await query
                .OrderByDescending(p => p.Id) // Show newest first
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .Include(p => p.PriceHistory)
                .ToListAsync();
        }

        public async Task<int> GetTotalCountAsync(string? searchTerm)
        {
            var query = _context.Products.AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(p => p.Name.ToLower().Contains(searchTerm.ToLower()));
            }

            return await query.CountAsync();
        }
    }
}
