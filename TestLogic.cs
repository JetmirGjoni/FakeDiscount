using System;
using System.Collections.Generic;
using System.Linq;

public class Program 
{
    public static void Main()
    {
       
        var history = new List<decimal>();
        for (int i = 0; i < 10; i++) history.Add(243.5m);

        decimal currentPrice = 79.5m;
        
        
        decimal? originalPrice1 = 243.5m;
        bool isFake1 = IsFakeDiscount(history, currentPrice, originalPrice1);
        Console.WriteLine($"Case 1 (Claimed Original 243.5): Flagged? {isFake1}");

       
        decimal? originalPrice2 = 400m;
        bool isFake2 = IsFakeDiscount(history, currentPrice, originalPrice2);
        Console.WriteLine($"Case 2 (Claimed Original 400.0): Flagged? {isFake2}");
        
       
        decimal? originalPrice3 = 300m;
        bool isFake3 = IsFakeDiscount(history, currentPrice, originalPrice3);
        Console.WriteLine($"Case 3 (Claimed Original 300.0): Flagged? {isFake3}");
    }

    public static bool IsFakeDiscount(List<decimal> history, decimal currentPrice, decimal? originalPrice)
    {
        if (originalPrice == null || originalPrice <= currentPrice) return false;

        var recentHistory = history.Take(10).ToList(); // User reverted to 10

        if (recentHistory.Any())
        {
            var avgPrice = recentHistory.Average();
            Console.WriteLine($"Debug: Avg: {avgPrice}, Threshold (Avg*1.2): {avgPrice * 1.2m}, Claimed: {originalPrice}");
            
            if (originalPrice.Value > avgPrice * 1.2m)
            {
                return true;
            }
        }
        return false;
    }
}
