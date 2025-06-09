using CreateRoadmapADO.Models;

namespace CreateRoadmapADO.Interfaces;

/// <summary>
/// Interface for roadmap generation and management
/// </summary>
public interface IRoadmapService
{
    /// <summary>
    /// Generates a roadmap from work items
    /// </summary>
    /// <param name="workItems">Collection of work items</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of roadmap items</returns>
    Task<IEnumerable<RoadmapItem>> GenerateRoadmapAsync(IEnumerable<WorkItem> workItems, CancellationToken cancellationToken = default);

    /// <summary>
    /// Converts a work item to a roadmap item
    /// </summary>
    /// <param name="workItem">Work item to convert</param>
    /// <returns>Converted roadmap item</returns>
    RoadmapItem ConvertToRoadmapItem(WorkItem workItem);

    /// <summary>
    /// Sorts roadmap items by priority and dependencies
    /// </summary>
    /// <param name="roadmapItems">Roadmap items to sort</param>
    /// <returns>Sorted roadmap items</returns>
    IEnumerable<RoadmapItem> SortRoadmapItems(IEnumerable<RoadmapItem> roadmapItems);

    /// <summary>
    /// Creates an Epic work item with child items
    /// </summary>
    /// <param name="children">IDs of child work items</param>
    /// <param name="title">Title of the Epic</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task CreateEpicAsync(List<int> children, string title);
    
    /// <summary>
    /// Creates a Release Train work item with child items
    /// </summary>
    /// <param name="children">IDs of child work items</param>
    /// <param name="title">Title of the Release Train</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task CreateReleaseTrainAsync(List<int> children, string title);
}
