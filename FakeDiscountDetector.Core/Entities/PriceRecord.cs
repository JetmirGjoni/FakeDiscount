using System;

namespace FakeDiscountDetector.Core.Entities
{
    public class PriceRecord
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal Price { get; set; }
        public decimal? OriginalPrice { get; set; }
        public DateTime Timestamp { get; set; }
        
        // Navigation property
        public Product Product { get; set; } = null!;
    }
}
