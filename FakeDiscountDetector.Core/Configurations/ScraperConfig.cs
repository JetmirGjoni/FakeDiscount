namespace FakeDiscountDetector.Core.Configurations
{
    public class ScraperConfig
    {
        public string Name { get; set; } = string.Empty;
        public string BaseUrl { get; set; } = string.Empty;
        public string WaitSelector { get; set; } = string.Empty;

        // "LoadMoreButton" is the only supported one for now, but good to have the field
        public string PaginationType { get; set; } = "LoadMoreButton";
        public string PaginationSelector { get; set; } = string.Empty;
        public int MaxPages { get; set; } = 5;

        public string ItemSelector { get; set; } = string.Empty;
        public string NameSelector { get; set; } = string.Empty;
        public string PriceSelector { get; set; } = string.Empty;
        public string OldPriceSelector { get; set; } = string.Empty;
        public string ImageSelector { get; set; } = string.Empty;

        // Web Crawling Properties
        public string? CategorySelector { get; set; }
        public string? PriceCulture { get; set; } // e.g. "de-DE", "en-US"
        public string? TargetUrl { get; set; }

        public decimal PriceMultiplier { get; set; } = 1.0m;
        public decimal OldPriceMultiplier { get; set; } = 1.0m;
    }
}
