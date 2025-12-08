using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;
using Microsoft.ML;

namespace FakeDiscountDetector.Infrastructure.AI
{
    public class MLProductClassifier : IProductClassifier
    {
        private readonly MLContext _mlContext;
        private readonly string _modelPath;
        private ITransformer? _model;
        private PredictionEngine<ProductEntry, CategoryPrediction>? _predictionEngine;

        public MLProductClassifier()
        {
            _mlContext = new MLContext(seed: 0);
            _modelPath = Path.Combine(AppContext.BaseDirectory, "model.zip");
        }

        private void LoadModel()
        {
            if (_model == null)
            {
                if (File.Exists(_modelPath))
                {
                    _model = _mlContext.Model.Load(_modelPath, out var modelInputSchema);
                    _predictionEngine = _mlContext.Model.CreatePredictionEngine<ProductEntry, CategoryPrediction>(_model);
                }
                else
                {

                }
            }
        }

        public Task<string> PredictCategoryAsync(Product product)
        {
            LoadModel();
            if (_predictionEngine == null) return Task.FromResult("Unknown");

            var prediction = _predictionEngine.Predict(new ProductEntry
            {
                Name = product.Name,
                StoreName = product.StoreName,
                Price = (float)(product.PriceHistory.OrderByDescending(ph => ph.Timestamp).FirstOrDefault()?.Price ?? 0)
            });

            return Task.FromResult(prediction.Category);
        }

        public Task<(string Category, float Confidence)> PredictCategoryWithConfidenceAsync(Product product)
        {
            LoadModel();
            if (_predictionEngine == null) return Task.FromResult(("Unknown", 0f));

            var prediction = _predictionEngine.Predict(new ProductEntry
            {
                Name = product.Name,
                StoreName = product.StoreName,
                Price = (float)(product.PriceHistory.OrderByDescending(ph => ph.Timestamp).FirstOrDefault()?.Price ?? 0)
            });



            float maxScore = prediction.Score != null && prediction.Score.Length > 0 ? prediction.Score.Max() : 0f;

            return Task.FromResult((prediction.Category, maxScore));
        }
    }
}
