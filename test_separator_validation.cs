using System;

public class SeparatorPatternTester
{
    public static void Main()
    {
        // Test cases that SHOULD be detected as separators
        string[] shouldBeSeparators = {
            "----------------------------- CY25 -----------------------------",
            "%%%%%%%% Q1 FY25 %%%%%%%%",
            "---- Sprint Planning ----",
            "%%%%%%% 2025 %%%%%%%",
            "--------%%%%--------",
            "--------------------- CY25H2 End ---------------------"
        };

        // Test cases that should NOT be detected as separators
        string[] shouldNotBeSeparators = {
            "Feature - User Authentication",
            "Release Train Alpha",
            "Q1-2025 Planning Initiative",
            "CY25 Budget Approval Process",
            "Sprint-1 Development Tasks"
        };

        Console.WriteLine("Testing separator pattern detection:");
        Console.WriteLine();

        Console.WriteLine("SHOULD BE SEPARATORS:");
        foreach (var title in shouldBeSeparators)
        {
            bool result = IsSeparatorPattern(title);
            Console.WriteLine($"  {result} : \"{title}\"");
        }

        Console.WriteLine();
        Console.WriteLine("SHOULD NOT BE SEPARATORS:");
        foreach (var title in shouldNotBeSeparators)
        {
            bool result = IsSeparatorPattern(title);
            Console.WriteLine($"  {result} : \"{title}\"");
        }
    }

    private static bool IsSeparatorPattern(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var cleanTitle = title.Trim();
        
        // Check if title contains mostly dashes and/or percent signs with minimal text content
        // Pattern examples: "-------- CY25 --------", "%%%%% Q1 %%%%%", "--- FY25 ---"
        var dashCount = cleanTitle.Count(c => c == '-');
        var percentCount = cleanTitle.Count(c => c == '%');
        var separatorCount = dashCount + percentCount;
        
        // If more than 60% of the title is separators (dashes/percents), consider it a separator pattern
        var separatorRatio = (double)separatorCount / cleanTitle.Length;
        
        return separatorRatio > 0.6;
    }
}
