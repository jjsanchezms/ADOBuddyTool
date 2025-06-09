using CreateRoadmapADO.Models;

namespace CreateRoadmapADO.Interfaces;

/// <summary>
/// Interface for output generation in various formats
/// </summary>
public interface IOutputService
{
    /// <summary>
    /// Exports roadmap items to JSON format
    /// </summary>
    /// <param name="roadmapItems">Roadmap items to export</param>
    /// <param name="filePath">Output file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task ExportToJsonAsync(IEnumerable<RoadmapItem> roadmapItems, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports roadmap items to CSV format
    /// </summary>
    /// <param name="roadmapItems">Roadmap items to export</param>
    /// <param name="filePath">Output file path</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task representing the asynchronous operation</returns>
    Task ExportToCsvAsync(IEnumerable<RoadmapItem> roadmapItems, string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Displays roadmap items in console
    /// </summary>
    /// <param name="roadmapItems">Roadmap items to display</param>
    void DisplayInConsole(IEnumerable<RoadmapItem> roadmapItems);
}
