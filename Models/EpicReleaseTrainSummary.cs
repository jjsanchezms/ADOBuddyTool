using System.ComponentModel;

namespace CreateRoadmapADO.Models;

/// <summary>
/// Summary of epic/release train creation and update operations
/// </summary>
public class EpicReleaseTrainSummary
{
    public List<EpicReleaseTrainOperation> Operations { get; set; } = new();
    public int TotalBacklogItemsProcessed { get; set; }
    public bool BacklogReadSuccessfully { get; set; }
}

/// <summary>
/// Represents a single epic or release train operation
/// </summary>
public class EpicReleaseTrainOperation
{
    public string Type { get; set; } = string.Empty; // "Epic" or "Release Train"
    public OperationType Operation { get; set; }
    public string Title { get; set; } = string.Empty;
    public int Id { get; set; }
    public int TotalWorkItems { get; set; }
    public int NewRelationsAdded { get; set; }
}

/// <summary>
/// Type of operation performed
/// </summary>
public enum OperationType
{
    Created,
    Updated
}
