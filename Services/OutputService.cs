using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Service for output generation in various formats
/// </summary>
public class OutputService : IOutputService
{
    private readonly ILogger<OutputService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public OutputService(ILogger<OutputService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    public async Task ExportToJsonAsync(IEnumerable<RoadmapItem> roadmapItems, string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Exporting {Count} roadmap items to JSON: {FilePath}", roadmapItems.Count(), filePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var json = JsonSerializer.Serialize(roadmapItems, _jsonOptions);
            await File.WriteAllTextAsync(filePath, json, cancellationToken);

            _logger.LogInformation("Successfully exported roadmap to JSON file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting roadmap to JSON file: {FilePath}", filePath);
            throw;
        }
    }
    public async Task ExportToCsvAsync(IEnumerable<RoadmapItem> roadmapItems, string filePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Exporting {Count} roadmap items to CSV: {FilePath}", roadmapItems.Count(), filePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var csv = new StringBuilder();

            // Header
            csv.AppendLine("Id,Title,Description,Type,Status,AssignedTo,StartDate,EndDate,Priority,StackRank,Dependencies,Tags");

            // Data rows
            foreach (var item in roadmapItems)
            {
                csv.AppendLine($"{item.Id}," +
                              $"\"{EscapeCsvValue(item.Title)}\"," +
                              $"\"{EscapeCsvValue(item.Description)}\"," +
                              $"{item.Type}," +
                              $"{item.Status}," +
                              $"\"{EscapeCsvValue(item.AssignedTo ?? string.Empty)}\"," +
                              $"{item.StartDate:yyyy-MM-dd}," +
                              $"{item.EndDate:yyyy-MM-dd}," +
                              $"{item.Priority}," +
                              $"{item.StackRank:F1}," +
                              $"\"{string.Join(";", item.Dependencies)}\"," +
                              $"\"{string.Join(";", item.Tags)}\"");
            }

            await File.WriteAllTextAsync(filePath, csv.ToString(), cancellationToken);

            _logger.LogInformation("Successfully exported roadmap to CSV file: {FilePath}", filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting roadmap to CSV file: {FilePath}", filePath);
            throw;
        }
    }
    public void DisplayInConsole(IEnumerable<RoadmapItem> roadmapItems)
    {
        try
        {
            var items = roadmapItems.ToList();
            _logger.LogInformation("Displaying {Count} roadmap items in console", items.Count);

            Console.WriteLine();
            Console.WriteLine("=== ROADMAP (Sorted by StackRank) ===");
            Console.WriteLine();

            if (!items.Any())
            {
                Console.WriteLine("No roadmap items found.");
                return;
            }            // Display table header with improved clarity
            Console.WriteLine($"{"ID",-5} {"Title",-40}");
            Console.WriteLine("(Lower StackRank values appear first, items with N/A appear last)"); Console.WriteLine(new string('=', 80));

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

        // Show additional details only if they have values
        if (item.Priority.HasValue)
        {
            Console.WriteLine($"      Priority: {item.Priority}");
        }
        else
        {
            Console.WriteLine($"      Priority: Not set");
        }

        // Always show StackRank info with details about how it affects sorting
        Console.WriteLine($"      StackRank: {(item.StackRank.HasValue ? $"{item.StackRank:F2} (lower values appear first)" : "Not set - will appear at the end")}");

        if (item.StartDate.HasValue)
        {
            Console.WriteLine($"      Timeline: {item.StartDate:yyyy-MM-dd} to {item.EndDate:yyyy-MM-dd}");
        }


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

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        // Escape quotes by doubling them
        return value.Replace("\"", "\"\"");
    }
}
