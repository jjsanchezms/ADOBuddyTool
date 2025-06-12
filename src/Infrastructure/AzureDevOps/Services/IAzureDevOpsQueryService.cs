using ADOBuddyTool.Domain.Entities;

namespace ADOBuddyTool.Infrastructure.AzureDevOps.Services;

/// <summary>
/// Service interface for Azure DevOps work item queries
/// </summary>
public interface IAzureDevOpsQueryService : IDisposable
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
    /// Retrieves both Feature and Release Train work items for SWAG updates, including closed Features
    /// </summary>
    /// <param name="limit">Maximum number of work items to retrieve</param>
    /// <param name="areaPath">Area path to filter work items</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of Feature and Release Train work items including closed Features</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsForSwagUpdatesAsync(int limit, string areaPath, CancellationToken cancellationToken = default);

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
    /// Retrieves work items by WIQL query
    /// </summary>
    /// <param name="wiqlQuery">WIQL query string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of work items</returns>
    Task<IEnumerable<WorkItem>> GetWorkItemsByQueryAsync(string wiqlQuery, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a work item has a related auto-generated Release Train
    /// </summary>
    /// <param name="workItemId">Work item ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the existing related item, or 0 if none exists</returns>
    Task<int> GetExistingRelatedParentItemIdAsync(int workItemId, CancellationToken cancellationToken = default);
}
