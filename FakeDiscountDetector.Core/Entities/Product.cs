using System;
using System.Collections.Generic;

namespace FakeDiscountDetector.Core.Entities
{
    public class Product
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // "Gjirafa50", "Foleja", etc.

        // For product matching
        public Guid? GroupId { get; set; }
        public string StoreName { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;

        public string Category { get; set; } = string.Empty;

        // Navigation property
        public List<PriceRecord> PriceHistory { get; set; } = new List<PriceRecord>();
    }
}
