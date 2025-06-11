using CreateRoadmapADO.Models;

namespace CreateRoadmapADO.Interfaces;

/// <summary>
/// Interface for Azure DevOps API operations
/// </summary>
public interface IAzureDevOpsService : IDisposable
{
    /// <summary>
    /// Retrieves Feature work items from Azure DevOps
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of Feature work items</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsAsync(CancellationToken cancellationToken = default);    /// <summary>
    /// Retrieves Feature work items from Azure DevOps with limit
    /// </summary>
    /// <param name="limit">Maximum number of work items to retrieve</param>
    /// <param name="areaPath">Area path to filter work items</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of Feature work items</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsAsync(int limit, string areaPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves work items from Azure DevOps with limit and work item type
    /// </summary>
    /// <param name="limit">Maximum number of work items to retrieve</param>
    /// <param name="areaPath">Area path to filter work items</param>
    /// <param name="workItemType">Type of work items to retrieve (e.g., 'Feature', 'Release Train')</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of work items of the specified type</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsAsync(int limit, string areaPath, string workItemType, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves both Feature and Release Train work items for hygiene checks
    /// </summary>
    /// <param name="limit">Maximum number of work items to retrieve</param>
    /// <param name="areaPath">Area path to filter work items</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of Feature and Release Train work items</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsForHygieneChecksAsync(int limit, string areaPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific work item by ID
    /// </summary>
    /// <param name="workItemId">Work item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Work item details</returns>
    Task<WorkItem?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a specific work item by ID with its relations
    /// </summary>
    /// <param name="workItemId">Work item ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Work item details including relations</returns>
    Task<WorkItem?> GetWorkItemWithRelationsAsync(int workItemId, CancellationToken cancellationToken = default);    /// <summary>
    /// Checks if a work item has a related auto-generated Release Train
    /// </summary>
    /// <param name="workItemId">Work item ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the existing related item, or 0 if none exists</returns>
    Task<int> GetExistingRelatedParentItemIdAsync(int workItemId, CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves work items by WIQL query
    /// </summary>
    /// <param name="wiqlQuery">WIQL query string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of work items</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsByQueryAsync(string wiqlQuery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Release Train work item with child items
    /// </summary>
    /// <param name="children">IDs of child work items</param>
    /// <param name="title">Title of the Release Train</param>
    /// <param name="patternItemId">ID of the pattern work item that triggered this creation</param>
    /// <returns>The ID of the created Release Train work item</returns>
    Task<int> CreateReleaseTrainAsync(List<int> children, string title, int patternItemId = 0);
    
    /// <summary>
    /// Updates a work item's title
    /// </summary>    /// <param name="workItemId">ID of the work item to update</param>
    /// <param name="newTitle">The new title for the work item</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task UpdateWorkItemTitleAsync(int workItemId, string newTitle);
    
    /// <summary>
    /// Creates a specific relation between two work items
    /// </summary>
    /// <param name="sourceId">ID of the source work item</param>
    /// <param name="targetId">ID of the target work item to link to</param>
    /// <param name="comment">Optional comment for the relation</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task CreateRelationAsync(int sourceId, int targetId, string comment = "");
}
