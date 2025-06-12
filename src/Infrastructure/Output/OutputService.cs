using ADOBuddyTool.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ADOBuddyTool.Infrastructure.Output;

/// <summary>
/// Service for output generation in console format
/// </summary>
public class OutputService
{
    private readonly ILogger<OutputService> _logger;

    public OutputService(ILogger<OutputService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public void DisplayInConsole(IEnumerable<RoadmapItem> roadmapItems)
    {
        try
        {
            var items = roadmapItems.ToList();
            _logger.LogInformation("Displaying {Count} roadmap items in console", items.Count);

            // Keep all console output for roadmap display - this is user-facing output
            Console.WriteLine();
            Console.WriteLine("=== ROADMAP (Sorted by StackRank) ===");
            Console.WriteLine();

            if (!items.Any())
            {
                Console.WriteLine("No roadmap items found.");
                return;
            }

            // Display table header with improved clarity
            Console.WriteLine($"{"ID",-5} {"StackRank",-12} {"Type",-10} {"Status",-12} {"Title",-40}");
            Console.WriteLine("(Lower StackRank values appear first, items with N/A appear last)");
            Console.WriteLine(new string('=', 80));

            foreach (var item in items)
            {
                DisplayRoadmapItem(item);
                Console.WriteLine(new string('-', 80));
            }

            Console.WriteLine();
            Console.WriteLine($"Total Items: {items.Count}");
            Console.WriteLine($"Not Started: {items.Count(i => i.Status == RoadmapItemStatus.NotStarted)}");
            Console.WriteLine($"In Progress: {items.Count(i => i.Status == RoadmapItemStatus.InProgress)}");
            Console.WriteLine($"Completed: {items.Count(i => i.Status == RoadmapItemStatus.Completed)}");
            Console.WriteLine($"Blocked: {items.Count(i => i.Status == RoadmapItemStatus.Blocked)}");
            Console.WriteLine($"Cancelled: {items.Count(i => i.Status == RoadmapItemStatus.Cancelled)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error displaying roadmap in console");
            throw;
        }
    }

    private static void DisplayRoadmapItem(RoadmapItem item)
    {
        // Format StackRank with more detail and highlight if it's missing
        string stackRank;
        if (item.StackRank.HasValue)
        {
            stackRank = $"{item.StackRank:F2}";
            // Make it more visible with some formatting
            if (item.StackRank.Value == 0)
            {
                stackRank = "0.00 (!)"; // Emphasize zero values
            }
        }
        else
        {
            stackRank = "N/A (!)"; // Make missing values stand out
        }
        // Format display in table-like structure
        Console.WriteLine($"{item.Id,-5} {stackRank,-12} {item.Type,-10} {item.Status,-12} {TruncateString(item.Title, 40),-40}");

        // Show description on next line with indentation
        Console.WriteLine($"      Description: {TruncateString(item.Description, 72)}");

        // Always show StackRank info with details about how it affects sorting
        Console.WriteLine($"      StackRank: {(item.StackRank.HasValue ? $"{item.StackRank:F2} (lower values appear first)" : "Not set - will appear at the end")}");

        if (item.Dependencies.Any())
        {
            Console.WriteLine($"      Dependencies: {string.Join(", ", item.Dependencies)}");
        }
    }
    private static string TruncateString(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value)) return string.Empty;
        return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Exports hygiene check results to a file
    /// </summary>
    /// <param name="hygieneResults">Hygiene check results to export</param>
    /// <param name="filePath">Path to export the file to</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task ExportHygieneCheckResultsAsync(HygieneCheckSummary hygieneResults, string filePath, CancellationToken cancellationToken = default)
    {
        // CSV export functionality has been removed
        // Only console display is now supported for hygiene check results
        _logger.LogWarning("Export functionality for hygiene check results has been removed. Results are displayed in console only.");
        await Task.CompletedTask;
    }
}

