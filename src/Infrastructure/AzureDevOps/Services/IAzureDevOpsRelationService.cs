namespace ADOBuddyTool.Infrastructure.AzureDevOps.Services;

/// <summary>
/// Service interface for Azure DevOps work item relationships
/// </summary>
public interface IAzureDevOpsRelationService : IDisposable
{
    /// <summary>
    /// Creates a specific relation between two work items
    /// </summary>
    /// <param name="sourceId">ID of the source work item</param>
    /// <param name="targetId">ID of the target work item to link to</param>
    /// <param name="comment">Optional comment for the relation</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task CreateRelationAsync(int sourceId, int targetId, string comment = "");

    /// <summary>
    /// Checks if a work item has a related auto-generated Release Train
    /// </summary>
    /// <param name="workItemId">Work item ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the existing related item, or 0 if none exists</returns>
    Task<int> GetExistingRelatedParentItemIdAsync(int workItemId, CancellationToken cancellationToken = default);
}
