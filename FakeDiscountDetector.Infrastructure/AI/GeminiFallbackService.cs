using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FakeDiscountDetector.Infrastructure.AI
{
    public class GeminiFallbackService : IProductClassifier
    {
        private readonly string _apiKey;
        private readonly HttpClient _httpClient;
        private const string GEMINI_API_URL = "https://generativelanguage.googleapis.com/v1/models/gemini-1.5-flash:generateContent";

        public GeminiFallbackService(IConfiguration configuration, HttpClient httpClient)
        {
            _apiKey = configuration["Gemini:ApiKey"] ?? string.Empty;
            _httpClient = httpClient;
        }

        public async Task<string> PredictCategoryAsync(Product product)
        {
            if (string.IsNullOrEmpty(_apiKey)) return "Unknown (No API Key)";

            var prompt = $"Classify the following product into a single category. Return ONLY the category name from the following list:Smartphone,Tablet,Laptop,Other,Computer/Component,Smart Home/Camera,Smartwatch,Monitor,Furniture, Audio,TV,Gaming, nothing else.\n\n" +
                         $"Product: {product.Name}\n" +
                         $"Store: {product.StoreName}\n" +
                         $"Price: {product.PriceHistory.FindLast(x => true)?.Price}";

            var requestBody = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = prompt }
                        }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 50
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync($"{GEMINI_API_URL}?key={_apiKey}", content);

                var responseString = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Gemini API Error: {response.StatusCode}");
                    Console.WriteLine($"Response: {responseString}");
                    return "Unknown (Error)";
                }

                using var doc = JsonDocument.Parse(responseString);

                // Navigate the JSON response si candidates[0].content.parts[0].text
                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text?.Trim() + " - From Gemini" ?? "Unknown";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Gemini Error: {ex.Message}");
                return "Unknown (Error)";
            }
        }

        public async Task<(string Category, float Confidence)> PredictCategoryWithConfidenceAsync(Product product)
        {
            var category = await PredictCategoryAsync(product);
            
            return (category, category != "Unknown" && !category.Contains("Error") ? 1.0f : 0.0f);
        }
    }
}
