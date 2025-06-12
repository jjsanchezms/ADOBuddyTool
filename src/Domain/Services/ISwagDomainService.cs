using CreateRoadmapADO.Domain.Entities;

namespace CreateRoadmapADO.Domain.Services;

/// <summary>
/// Domain service interface for SWAG (estimation) business operations
/// </summary>
public interface ISwagDomainService
{
    /// <summary>
    /// Extracts SWAG value from various sources (field, status notes, etc.)
    /// </summary>
    /// <param name="workItem">Work item to extract SWAG from</param>
    /// <returns>SWAG value if found</returns>
    double? ExtractSwagValue(WorkItem workItem);

    /// <summary>
    /// Formats SWAG value for display and storage
    /// </summary>
    /// <param name="swagValue">SWAG value to format</param>
    /// <returns>Formatted SWAG string</returns>
    string FormatSwagValue(double swagValue);

    /// <summary>
    /// Creates SWAG prefix for status notes
    /// </summary>
    /// <param name="swagValue">SWAG value</param>
    /// <param name="existingStatusNotes">Existing status notes</param>
    /// <returns>Status notes with SWAG prefix</returns>
    string CreateSwagPrefixedStatusNotes(double swagValue, string existingStatusNotes);

    /// <summary>
    /// Removes SWAG prefix from status notes
    /// </summary>
    /// <param name="statusNotes">Status notes with potential SWAG prefix</param>
    /// <returns>Clean status notes without SWAG prefix</returns>
    string RemoveSwagPrefix(string statusNotes);

    /// <summary>
    /// Validates SWAG value consistency across work item sources
    /// </summary>
    /// <param name="workItem">Work item to validate</param>
    /// <returns>Validation result</returns>
    SwagValidationResult ValidateSwagConsistency(WorkItem workItem);

    /// <summary>
    /// Determines if SWAG update is needed
    /// </summary>
    /// <param name="currentSwag">Current SWAG value</param>
    /// <param name="calculatedSwag">Calculated SWAG value</param>
    /// <param name="tolerance">Acceptable difference tolerance</param>
    /// <returns>True if update is needed</returns>
    bool IsSwagUpdateNeeded(double? currentSwag, double calculatedSwag, double tolerance = 0.1);
}

/// <summary>
/// Result of SWAG validation
/// </summary>
public class SwagValidationResult
{
    public bool IsConsistent { get; set; }
    public double? FieldValue { get; set; }
    public double? StatusNotesValue { get; set; }
    public List<string> Issues { get; set; } = new();
    public SwagValidationSeverity Severity { get; set; }
}

/// <summary>
/// Severity levels for SWAG validation issues
/// </summary>
public enum SwagValidationSeverity
{
    Info,
    Warning,
    Error
}
