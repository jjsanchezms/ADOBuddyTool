using System;

namespace CreateRoadmapADO
{
    /// <summary>
    /// Simple test to demonstrate the SWAG update behavior for empty status notes
    /// </summary>
    public class SwagUpdateTest
    {
        public static void TestSwagUpdateLogic()
        {
            Console.WriteLine("Testing SWAG Update Logic for Empty Status Notes");
            Console.WriteLine("=".PadRight(60, '='));            // Test scenarios
            var scenarios = new[]
            {
                new { Description = "null status notes", StatusNotes = (string?)null },
                new { Description = "empty status notes", StatusNotes = (string?)"" },
                new { Description = "whitespace status notes", StatusNotes = (string?)"   " },
                new { Description = "existing SWAG only", StatusNotes = (string?)"[SWAG: 5]" },
                new { Description = "SWAG with content", StatusNotes = (string?)"[SWAG: 3]This is existing content" },
                new { Description = "no SWAG, has content", StatusNotes = (string?)"This is existing content without SWAG" }
            };

            double swagValue = 10.5;

            foreach (var scenario in scenarios)
            {
                Console.WriteLine($"\nScenario: {scenario.Description}");
                Console.WriteLine($"Original: '{scenario.StatusNotes ?? "null"}'");

                var result = SimulateSwagUpdate(scenario.StatusNotes, swagValue);
                Console.WriteLine($"Result:   '{result}'");
            }
        }
        private static string SimulateSwagUpdate(string? originalStatusNotes, double swagValue)
        {
            // Simulate the logic from UpdateWorkItemStatusNotesWithSwagAsync
            var cleanStatusNotes = RemoveSwagPrefixFromDescription(originalStatusNotes);

            // If status notes are empty after cleaning, create a meaningful SWAG-only message
            string newStatusNotes;
            if (string.IsNullOrWhiteSpace(cleanStatusNotes))
            {
                newStatusNotes = $"[SWAG: {swagValue}] Total effort estimate based on sum of related Features.";
            }
            else
            {
                newStatusNotes = $"[SWAG: {swagValue}]{cleanStatusNotes}";
            }

            return newStatusNotes;
        }

        private static string? RemoveSwagPrefixFromDescription(string? description)
        {
            if (string.IsNullOrEmpty(description))
                return description;

            // Look for pattern [SWAG: number] at the beginning
            var pattern = @"^\[SWAG:\s*\d+(?:\.\d+)?\]";
            var regex = new System.Text.RegularExpressions.Regex(pattern);

            return regex.Replace(description, "").TrimStart();
        }
    }
}
