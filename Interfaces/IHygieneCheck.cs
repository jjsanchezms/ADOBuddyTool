using CreateRoadmapADO.Models;

namespace CreateRoadmapADO.Interfaces;

/// <summary>
/// Interface for hygiene check implementations
/// </summary>
public interface IHygieneCheck
{
    /// <summary>
    /// Performs the hygiene check on the specified work item
    /// </summary>
    /// <param name="context">Context containing work item and related data for the check</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>Collection of hygiene check results</returns>
    Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the name of this hygiene check
    /// </summary>
    string CheckName { get; }

    /// <summary>
    /// Gets the description of what this hygiene check validates
    /// </summary>
    string CheckDescription { get; }
}

/// <summary>
/// Context object containing all data needed for hygiene checks
/// </summary>
public class HygieneCheckContext
{
    /// <summary>
    /// The primary work item being checked
    /// </summary>
    public required WorkItem WorkItem { get; set; }

    /// <summary>
    /// Related features for Release Train checks
    /// </summary>
    public List<WorkItem> RelatedFeatures { get; set; } = new();

    /// <summary>
    /// All Release Trains for Feature checks
    /// </summary>
    public List<WorkItem> AllReleaseTrains { get; set; } = new();

    /// <summary>
    /// Generates the Azure DevOps URL for a work item
    /// </summary>
    /// <param name="workItemId">Work item ID</param>
    /// <returns>Full URL to the work item in Azure DevOps</returns>
    public static string GenerateWorkItemUrl(int workItemId)
    {
        const string azureDevOpsBaseUrl = "https://skype.visualstudio.com/SPOOL/_workitems/edit";
        return $"{azureDevOpsBaseUrl}/{workItemId}";
    }
}
