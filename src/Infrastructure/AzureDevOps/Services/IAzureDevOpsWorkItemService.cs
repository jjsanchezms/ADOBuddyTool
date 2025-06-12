using ADOBuddyTool.Domain.Entities;

namespace ADOBuddyTool.Infrastructure.AzureDevOps.Services;

/// <summary>
/// Service interface for Azure DevOps work item modifications
/// </summary>
public interface IAzureDevOpsWorkItemService : IDisposable
{    /// <summary>
     /// Creates a Release Train work item with child items
     /// </summary>
     /// <param name="children">IDs of child work items</param>
     /// <param name="title">Title of the Release Train</param>
     /// <param name="areaPath">Area path for the Release Train</param>
     /// <param name="patternItemId">ID of the pattern work item that triggered this creation</param>
     /// <returns>The ID of the created Release Train work item</returns>
    Task<int> CreateReleaseTrainAsync(List<int> children, string title, string areaPath, int patternItemId = 0);

    /// <summary>
    /// Updates a work item's title
    /// </summary>
    /// <param name="workItemId">ID of the work item to update</param>
    /// <param name="newTitle">The new title for the work item</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task UpdateWorkItemTitleAsync(int workItemId, string newTitle);

    /// <summary>
    /// Updates a work item's SWAG (effort) value
    /// </summary>
    /// <param name="workItemId">ID of the work item to update</param>
    /// <param name="swagValue">The new SWAG value</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task UpdateWorkItemSwagAsync(int workItemId, double swagValue);

    /// <summary>
    /// Updates a work item's status notes with SWAG prefix
    /// </summary>
    /// <param name="workItemId">ID of the work item to update</param>
    /// <param name="swagValue">The SWAG value to add as prefix</param>
    /// <param name="originalStatusNotes">The original status notes</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task UpdateWorkItemStatusNotesWithSwagAsync(int workItemId, double swagValue, string originalStatusNotes);
}

