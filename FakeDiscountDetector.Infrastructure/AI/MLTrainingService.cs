using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace FakeDiscountDetector.Infrastructure.AI
{
    public class ProductEntry
    {
        [LoadColumn(0)]
        public string Name { get; set; } = string.Empty;

        [LoadColumn(1)]
        public string StoreName { get; set; } = string.Empty;

        [LoadColumn(2)]
        public float Price { get; set; }

        [LoadColumn(3)]
        public string Category { get; set; } = string.Empty;
    }

    public class CategoryPrediction
    {
        [ColumnName("PredictedLabel")]
        public string Category { get; set; } = string.Empty;

        public float[] Score { get; set; } = Array.Empty<float>();
    }

    public class MLTrainingService : ITrainingService
    {
        private readonly MLContext _mlContext;
        private readonly string _modelPath;

        public MLTrainingService()
        {
            _mlContext = new MLContext(seed: 0);
            _modelPath = Path.Combine(AppContext.BaseDirectory, "model.zip");
        }

        public Task TrainModelAsync(IEnumerable<Product> trainingData)
        {
            var data = trainingData.Select(p => new ProductEntry
            {
                Name = p.Name,
                StoreName = p.StoreName, // Using StoreName as context
                Price = (float)(p.PriceHistory.OrderByDescending(ph => ph.Timestamp).FirstOrDefault()?.Price ?? 0),
                Category = p.Category ?? "Uncategorized"
            });

            var dataView = _mlContext.Data.LoadFromEnumerable(data);

            var pipeline = _mlContext.Transforms.Conversion.MapValueToKey(inputColumnName: "Category", outputColumnName: "Label")
                .Append(_mlContext.Transforms.Text.FeaturizeText(inputColumnName: "Name", outputColumnName: "NameFeaturized"))
                .Append(_mlContext.Transforms.Text.FeaturizeText(inputColumnName: "StoreName", outputColumnName: "StoreNameFeaturized"))
                .Append(_mlContext.Transforms.Concatenate("Features", "NameFeaturized", "StoreNameFeaturized")) // We can add Price later if normalized
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy("Label", "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            var model = pipeline.Fit(dataView);

            _mlContext.Model.Save(model, dataView.Schema, _modelPath);

            return Task.CompletedTask;
        }

        public Task AddTrainingExampleAsync(Product product, string category)
        {


            var csvPath = Path.Combine(AppContext.BaseDirectory, "training_data.csv");
            var line = $"{product.Name},{product.StoreName},{product.PriceHistory.LastOrDefault()?.Price},{category}";

           
            File.AppendAllLines(csvPath, new[] { line });

            return Task.CompletedTask;
        }

        public async Task<IEnumerable<ProductEntry>> LoadFromCsvAsync(string csvPath)
        {
            if (!File.Exists(csvPath))
            {
                return Enumerable.Empty<ProductEntry>();
            }

            var entries = new List<ProductEntry>();
            var lines = await File.ReadAllLinesAsync(csvPath);

            // Skip header
            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i];
                if (string.IsNullOrWhiteSpace(line)) continue;

               
                var fields = ParseCsvLine(line);
                if (fields.Length >= 4)
                {
                    if (float.TryParse(fields[2], System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var price))
                    {
                        entries.Add(new ProductEntry
                        {
                            Name = fields[0],
                            StoreName = fields[1],
                            Price = price,
                            Category = fields[3]
                        });
                    }
                }
            }

            return entries;
        }

        private string[] ParseCsvLine(string line)
        {
            var fields = new List<string>();
            var inQuotes = false;
            var currentField = new System.Text.StringBuilder();

            for (int i = 0; i < line.Length; i++)
            {
                var c = line[i];

                if (c == '"')
                {
                    inQuotes = !inQuotes;
                }
                else if (c == ',' && !inQuotes)
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                }
                else
                {
                    currentField.Append(c);
                }
            }

            fields.Add(currentField.ToString().Trim());
            return fields.ToArray();
        }
    }
}
