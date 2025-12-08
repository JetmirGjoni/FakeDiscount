using System;

namespace FakeDiscountDetector.Core.Entities
{
    public class PricePrediction
    {
        public decimal PredictedPrice { get; set; }
        public DateTime PredictionDate { get; set; }
        public double ConfidenceScore { get; set; } // 0.0 to 1.0
        public string Trend { get; set; } = "Stable"; // "Up", "Down", "Stable"
        public string Advice { get; set; } = "Neutral"; // "Buy Now", "Wait", "Neutral"
    }
}
