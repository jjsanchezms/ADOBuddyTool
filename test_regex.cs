// Test regex pattern for release train recognition
using System;
using System.Text.RegularExpressions;

namespace TestRegex
{
    class Program
    {
        static void Main()
        {
            // Test the current regex pattern
            var pattern = @"^-+\s*(.*?)\s*-+rt(?::(\d+))?$";
            var regex = new Regex(pattern, RegexOptions.IgnoreCase);
            
            // Test cases
            string[] testTitles = {
                "----------- Relaibility CY25H1 ----------rt:4159254",
                "----- TITLE -----rt",
                "----- TITLE -----rt:1234",
                "--- Test ---rt:999"
            };
            
            Console.WriteLine("Testing regex pattern: " + pattern);
            Console.WriteLine();
            
            foreach (var title in testTitles)
            {
                var match = regex.Match(title);
                Console.WriteLine($"Title: '{title}'");
                Console.WriteLine($"  Match: {match.Success}");
                if (match.Success)
                {
                    Console.WriteLine($"  Group 1 (title): '{match.Groups[1].Value.Trim()}'");
                    if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                    {
                        Console.WriteLine($"  Group 2 (ID): '{match.Groups[2].Value}'");
                    }
                }
                Console.WriteLine();
            }
        }
    }
}
