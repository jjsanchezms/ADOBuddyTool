using System.Text.RegularExpressions;

namespace CreateRoadmapADO.Services.AzureDevOps;

/// <summary>
/// Service for SWAG-related operations
/// Focused on parsing and manipulating SWAG values in work item descriptions
/// </summary>
public class SwagService : ISwagService
{
    /// <summary>
    /// Extracts SWAG value from status notes if present
    /// </summary>
    /// <param name="description">The status notes to parse</param>
    /// <returns>SWAG value if found, null otherwise</returns>
    public double? ExtractSwagFromDescription(string description)
    {
        if (string.IsNullOrEmpty(description))
            return null;

        // Look for pattern [SWAG: number] at the beginning
        var pattern = @"^\[SWAG:\s*(\d+(?:\.\d+)?)\]";
        var regex = new Regex(pattern);
        var match = regex.Match(description);

        if (match.Success && double.TryParse(match.Groups[1].Value, out var swagValue))
        {
            return swagValue;
        }

        return null;
    }

    /// <summary>
    /// Removes existing SWAG prefix from status notes if present
    /// </summary>
    /// <param name="description">The status notes to clean</param>
    /// <returns>Status notes without SWAG prefix</returns>
    public string RemoveSwagPrefixFromDescription(string description)
    {
        if (string.IsNullOrEmpty(description))
            return description;

        // Look for pattern [SWAG: number] at the beginning
        var pattern = @"^\[SWAG:\s*\d+(?:\.\d+)?\]";
        var regex = new Regex(pattern);

        return regex.Replace(description, "").TrimStart();
    }
}
