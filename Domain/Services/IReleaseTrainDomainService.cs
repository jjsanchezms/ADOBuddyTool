using CreateRoadmapADO.Models;

namespace CreateRoadmapADO.Domain.Services;

/// <summary>
/// Domain service interface for Release Train business operations
/// </summary>
public interface IReleaseTrainDomainService
{
    /// <summary>
    /// Determines if a Release Train title follows the expected pattern
    /// </summary>
    /// <param name="title">Release Train title to validate</param>
    /// <returns>True if title follows pattern</returns>
    bool IsValidReleaseTrainTitle(string title);

    /// <summary>
    /// Generates a Release Train title based on pattern and features
    /// </summary>
    /// <param name="patternWorkItem">Pattern work item that triggered creation</param>
    /// <param name="features">Features to be included in the Release Train</param>
    /// <returns>Generated title</returns>
    string GenerateReleaseTrainTitle(WorkItem patternWorkItem, IEnumerable<WorkItem> features);

    /// <summary>
    /// Determines if features should be grouped into a Release Train
    /// </summary>
    /// <param name="features">Features to evaluate</param>
    /// <returns>Grouping recommendation</returns>
    ReleaseTrainGroupingResult ShouldGroupFeaturesIntoReleaseTrain(IEnumerable<WorkItem> features);

    /// <summary>
    /// Validates Release Train completeness and structure
    /// </summary>
    /// <param name="releaseTrain">Release Train to validate</param>
    /// <param name="relatedFeatures">Related features</param>
    /// <returns>Validation result</returns>
    ReleaseTrainValidationResult ValidateReleaseTrainCompleteness(WorkItem releaseTrain, IEnumerable<WorkItem> relatedFeatures);

    /// <summary>
    /// Determines if a Release Train should have SWAG updated automatically
    /// </summary>
    /// <param name="releaseTrain">Release Train to check</param>
    /// <param name="allMode">True if running in ALL mode</param>
    /// <returns>True if SWAG should be updated</returns>
    bool ShouldUpdateReleaseTrainSwag(WorkItem releaseTrain, bool allMode);
}

/// <summary>
/// Result of Release Train grouping analysis
/// </summary>
public class ReleaseTrainGroupingResult
{
    public bool ShouldGroup { get; set; }
    public string Reason { get; set; } = string.Empty;
    public int EstimatedFeatureCount { get; set; }
}

/// <summary>
/// Result of Release Train validation
/// </summary>
public class ReleaseTrainValidationResult
{
    public bool IsComplete { get; set; }
    public List<string> Issues { get; set; } = new();
    public int ExpectedFeatureCount { get; set; }
    public int ActualFeatureCount { get; set; }
}
