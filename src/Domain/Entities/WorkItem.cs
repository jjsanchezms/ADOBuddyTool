using System.Text.Json.Serialization;

namespace ADOBuddyTool.Domain.Entities;

/// <summary>
/// Represents a work item from Azure DevOps
/// </summary>
public class WorkItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    [JsonPropertyName("workItemType")]
    public string WorkItemType { get; set; } = string.Empty;

    [JsonPropertyName("stackRank")]
    public double? StackRank { get; set; }
    [JsonPropertyName("state")]
    public string State { get; set; } = string.Empty; [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("statusNotes")]
    public string StatusNotes { get; set; } = string.Empty;

    [JsonPropertyName("iterationPath")]
    public string? IterationPath { get; set; }

    [JsonPropertyName("tags")]
    public string Tags { get; set; } = string.Empty;
    [JsonPropertyName("relations")]
    public List<WorkItemRelation> Relations { get; set; } = new List<WorkItemRelation>();

    [JsonPropertyName("swag")]
    public double? Swag { get; set; }
}

/// <summary>
/// Represents a roadmap item that can be created from work items
/// </summary>
public class RoadmapItem
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public RoadmapItemType Type { get; set; }
    public RoadmapItemStatus Status { get; set; }
    public string? AssignedTo { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public int? Priority { get; set; }
    public double? StackRank { get; set; }
    public List<int> Dependencies { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

/// <summary>
/// Roadmap item types
/// </summary>
public enum RoadmapItemType
{
    ReleaseTrain,
    Feature,
    Initiative,
    Milestone
}

/// <summary>
/// Roadmap item status
/// </summary>
public enum RoadmapItemStatus
{
    NotStarted,
    InProgress,
    Completed,
    Blocked,
    Cancelled
}
