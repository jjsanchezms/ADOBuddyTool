using System;

// Test the title cleaning functionality
public class TitleCleaningTest
{
    public static void TestTitleCleaning()
    {
        Console.WriteLine("Testing Release Train Title Cleaning:");
        Console.WriteLine("=====================================");

        // Test cases
        var testCases = new[]
        {
            "----- ------------- GCCH ----------- -----",
            "---------- GCCH -----------",
            "--- Azure Security ---",
            "Performance Optimization",
            "--- --- CY25H1 --- ---",
            "   -----   Testing   -----   ",
            "---------------------------- Long Title ----------------------------"
        };

        foreach (var testCase in testCases)
        {
            var cleaned = CleanReleaseTrainTitle(testCase);
            Console.WriteLine($"Input:  '{testCase}'");
            Console.WriteLine($"Output: '{cleaned}'");
            Console.WriteLine();
        }
    }

    /// <summary>
    /// Cleans a release train title by removing excess dashes and whitespace
    /// </summary>
    /// <param name="rawTitle">The raw title extracted from the pattern</param>
    /// <returns>Clean title with just the core text</returns>
    private static string CleanReleaseTrainTitle(string rawTitle)
    {
        if (string.IsNullOrWhiteSpace(rawTitle))
            return string.Empty;

        // Remove leading and trailing dashes and whitespace
        // Handle patterns like "---------- GCCH -----------" -> "GCCH"
        var cleaned = rawTitle.Trim();

        // Remove leading dashes and spaces
        while (cleaned.Length > 0 && (cleaned[0] == '-' || char.IsWhiteSpace(cleaned[0])))
        {
            cleaned = cleaned.Substring(1);
        }

        // Remove trailing dashes and spaces
        while (cleaned.Length > 0 && (cleaned[cleaned.Length - 1] == '-' || char.IsWhiteSpace(cleaned[cleaned.Length - 1])))
        {
            cleaned = cleaned.Substring(0, cleaned.Length - 1);
        }

        return cleaned.Trim();
    }
}
