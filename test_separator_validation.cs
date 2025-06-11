using System;

namespace CreateRoadmapADO.Test;

public class SeparatorPatternTest
{    private static bool IsSeparatorPattern(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var cleanTitle = title.Trim();
        
        // Check if title starts with dashes (separator pattern)
        // Pattern examples: "--- Sprint Planning ---", "----------------------------- CY25 -----------------------------"
        return cleanTitle.StartsWith("---");
    }

    public static void Main()
    {
        var testCase = "--------------------- CY25 H1 HPA Items---------------------rt:4007257";
          Console.WriteLine($"Testing: {testCase}");
        Console.WriteLine($"Length: {testCase.Length}");
        Console.WriteLine($"Starts with '---': {testCase.StartsWith("---")}");
        Console.WriteLine($"Is separator pattern: {IsSeparatorPattern(testCase)}");
        
        // Test other examples
        var examples = new[]
        {
            "----------------------------- CY25 -----------------------------",
            "---- Sprint Planning ----",
            "--- FY25 ---",
            "%%%%%%%% Q1 FY25 %%%%%%%%",
            "Feature - User Authentication",
            "Release Train Alpha"
        };
        
        Console.WriteLine("\nTesting other examples:");
        foreach (var example in examples)
        {
            Console.WriteLine($"{example} -> {IsSeparatorPattern(example)}");
        }
    }
}
