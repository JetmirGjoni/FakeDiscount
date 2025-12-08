using System;
using System.Collections.Generic;
using System.Linq;
using FakeDiscountDetector.Core.Entities;
using FakeDiscountDetector.Core.Interfaces;

namespace FakeDiscountDetector.Infrastructure.Services
{
    public class TokenBasedProductMatcher : IProductMatcher
    {
        public bool AreMatch(Product productA, Product productB, double threshold = 0.5)
        {
            return CalculateSimilarity(productA, productB) >= threshold;
        }

        public double CalculateSimilarity(Product productA, Product productB)
        {
            if (string.IsNullOrWhiteSpace(productA.Name) || string.IsNullOrWhiteSpace(productB.Name))
                return 0.0;

            var tokensA = Tokenize(productA.Name);
            var tokensB = Tokenize(productB.Name);


            var intersection = tokensA.Intersect(tokensB).Count();
            var union = tokensA.Union(tokensB).Count();

            if (union == 0) return 0.0;

            return (double)intersection / union;
        }

        private HashSet<string> Tokenize(string text)
        {
            return text.ToLowerInvariant()
                .Split(new[] { ' ', '-', '_', ',', '.', '(', ')', '[', ']' }, StringSplitOptions.RemoveEmptyEntries)

                .ToHashSet();
        }
    }
}
