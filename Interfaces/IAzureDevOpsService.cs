using CreateRoadmapADO.Models;

namespace CreateRoadmapADO.Interfaces;

/// <summary>
/// Interface for Azure DevOps API operations
/// </summary>
public interface IAzureDevOpsService
{
    /// <summary>
    /// Retrieves Feature work items from Azure DevOps
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of Feature work items</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves Feature work items from Azure DevOps with limit
    /// </summary>
    /// <param name="limit">Maximum number of work items to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of Feature work items</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsAsync(int limit, CancellationToken cancellationToken = default);

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
    Task<WorkItem?> GetWorkItemWithRelationsAsync(int workItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a work item has a related auto-generated Epic or Release Train
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
    Task<IEnumerable<WorkItem>> GetWorkItemsByQueryAsync(string wiqlQuery, CancellationToken cancellationToken = default);    /// <summary>
    /// Creates an Epic work item with child items
    /// </summary>
    /// <param name="children">IDs of child work items</param>
    /// <param name="title">Title of the Epic</param>
    /// <param name="patternItemId">ID of the pattern work item that triggered this creation</param>
    /// <returns>The ID of the created Epic work item</returns>
    Task<int> CreateEpicAsync(List<int> children, string title, int patternItemId = 0);
    
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
    /// </summary>
    /// <param name="workItemId">ID of the work item to update</param>
    /// <param name="newTitle">The new title for the work item</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task UpdateWorkItemTitleAsync(int workItemId, string newTitle);
    
    /// <summary>
    /// Checks if a work item has an existing related auto-generated Epic or Release Train
    /// </summary>
    /// <param name="workItemId">ID of the work item to check</param>
    /// <param name="cancellationToken">Optional cancellation token</param>
    /// <returns>Task containing the ID of the existing parent item, or 0 if none exists</returns>
    Task<int> CheckForExistingParentAsync(int workItemId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a specific relation between two work items
    /// </summary>
    /// <param name="sourceId">ID of the source work item</param>
    /// <param name="targetId">ID of the target work item to link to</param>
    /// <param name="comment">Optional comment for the relation</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task CreateRelationAsync(int sourceId, int targetId, string comment = "");
}
