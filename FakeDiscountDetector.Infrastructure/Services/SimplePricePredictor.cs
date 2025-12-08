using System;
using System.Linq;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;

namespace FakeDiscountDetector.Infrastructure.Services
{
    public class SimplePricePredictor : IPricePredictor
    {
        public Task<PricePrediction> PredictNextPriceAsync(Product product)
        {
            if (product.PriceHistory == null || product.PriceHistory.Count < 2)
            {
                var currentPrice = product.PriceHistory?.LastOrDefault()?.Price ?? 0;
                return Task.FromResult(new PricePrediction
                {
                    PredictedPrice = currentPrice,
                    PredictionDate = DateTime.UtcNow.AddDays(7),
                    ConfidenceScore = 0.0,
                    Trend = "Unknown",
                    Advice = "Insufficient Data"
                });
            }

            var sortedHistory = product.PriceHistory.OrderBy(x => x.Timestamp).ToList();
            var currentPriceVal = sortedHistory.Last().Price;

            // Simple Linear Regression (Time vs Price)
            // x = days from start, y = price
            double n = sortedHistory.Count;
            double sumX = 0;
            double sumY = 0;
            double sumXY = 0;
            double sumXX = 0;

            var startDate = sortedHistory.First().Timestamp;

            foreach (var record in sortedHistory)
            {
                double x = (record.Timestamp - startDate).TotalDays;
                double y = (double)record.Price;

                sumX += x;
                sumY += y;
                sumXY += x * y;
                sumXX += x * x;
            }

            double slope = 0;
            if (Math.Abs(n * sumXX - sumX * sumX) > 0.0001)
            {
                slope = (n * sumXY - sumX * sumY) / (n * sumXX - sumX * sumX);
            }

            string trend = "Stable";
            if (slope > 0.1) trend = "Up";
            if (slope < -0.1) trend = "Down";

            // Predict price in 7 days
            double futureX = (DateTime.UtcNow.AddDays(7) - startDate).TotalDays;
            double intercept = (sumY - slope * sumX) / n;
            double predictedPriceDouble = slope * futureX + intercept;

            decimal predictedPrice = (decimal)Math.Max(0, predictedPriceDouble);

            string advice = "Neutral";
            if (trend == "Down") advice = "Wait";
            if (trend == "Up") advice = "Buy Now";
            if (predictedPrice < currentPriceVal * 0.9m) advice = "Wait";

            return Task.FromResult(new PricePrediction
            {
                PredictedPrice = predictedPrice,
                PredictionDate = DateTime.UtcNow.AddDays(7),
                ConfidenceScore = Math.Min(0.9, 0.1 * n), // More data = higher confidence
                Trend = trend,
                Advice = advice
            });
        }
    }
}
