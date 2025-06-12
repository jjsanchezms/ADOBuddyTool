namespace CreateRoadmapADO.Infrastructure.AzureDevOps.Services;

/// <summary>
/// Service interface for SWAG-related operations
/// </summary>
public interface ISwagService
{
    /// <summary>
    /// Extracts SWAG value from status notes if present
    /// </summary>
    /// <param name="description">The status notes to parse</param>
    /// <returns>SWAG value if found, null otherwise</returns>
    double? ExtractSwagFromDescription(string description);

    /// <summary>
    /// Removes existing SWAG prefix from status notes if present
    /// </summary>
    /// <param name="description">The status notes to clean</param>
    /// <returns>Status notes without SWAG prefix</returns>
    string RemoveSwagPrefixFromDescription(string description);
}
