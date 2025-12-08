using System;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;

namespace FakeDiscountDetector.Infrastructure.AI
{
    public class HybridClassifier : IProductClassifier
    {
        private readonly MLProductClassifier _localClassifier;
        private readonly GeminiFallbackService _fallbackClassifier;
        private readonly ITrainingService _trainingService;
        private const float CONFIDENCE_THRESHOLD = 0.7f;

        public HybridClassifier(
            MLProductClassifier localClassifier,
            GeminiFallbackService fallbackClassifier,
            ITrainingService trainingService)
        {
            _localClassifier = localClassifier;
            _fallbackClassifier = fallbackClassifier;
            _trainingService = trainingService;
        }

        public async Task<string> PredictCategoryAsync(Product product)
        {
            var (category, confidence) = await _localClassifier.PredictCategoryWithConfidenceAsync(product);

            if (confidence >= CONFIDENCE_THRESHOLD && category != "Unknown")
            {
                return category;
            }


            var fallbackCategory = await _fallbackClassifier.PredictCategoryAsync(product);

            if (fallbackCategory != "Unknown" && !fallbackCategory.Contains("Error"))
            {

                await _trainingService.AddTrainingExampleAsync(product, fallbackCategory);
                return fallbackCategory;
            }

            return category != "Unknown" ? category : "Uncategorized";
        }

        public async Task<(string Category, float Confidence)> PredictCategoryWithConfidenceAsync(Product product)
        {
            var (category, confidence) = await _localClassifier.PredictCategoryWithConfidenceAsync(product);

            if (confidence >= CONFIDENCE_THRESHOLD && category != "Unknown")
            {
                return (category, confidence);
            }


            var fallbackCategory = await _fallbackClassifier.PredictCategoryAsync(product);
            if (fallbackCategory != "Unknown" && !fallbackCategory.Contains("Error"))
            {

                await _trainingService.AddTrainingExampleAsync(product, fallbackCategory);
                return (fallbackCategory, 1.0f); 
            }

            return (category, confidence);
        }
    }
}
