using System.ComponentModel;

namespace ADOBuddyTool.Domain.Entities;

/// <summary>
/// Summary of release train creation and update operations
/// </summary>
public class ReleaseTrainSummary
{
    public List<ReleaseTrainOperation> Operations { get; set; } = new();
    public int TotalBacklogItemsProcessed { get; set; }
    public bool BacklogReadSuccessfully { get; set; }
}

/// <summary>
/// Represents a single release train operation
/// </summary>
public class ReleaseTrainOperation
{
    public string Type { get; set; } = "Release Train";
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
