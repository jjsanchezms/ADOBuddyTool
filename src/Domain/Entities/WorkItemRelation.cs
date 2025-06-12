using System.Text.Json.Serialization;

namespace ADOBuddyTool.Domain.Entities;

/// <summary>
/// Represents a relation between work items
/// </summary>
public class WorkItemRelation
{
    [JsonPropertyName("rel")]
    public string Rel { get; set; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("attributes")]
    public RelationAttributes? Attributes { get; set; }

    /// <summary>
    /// Extracts the work item ID from the URL
    /// </summary>
    /// <returns>Work item ID or 0 if not found</returns>
    public int GetRelatedWorkItemId()
    {
        if (string.IsNullOrEmpty(Url))
            return 0;

        // Extract the ID from the end of the URL
        var parts = Url.Split('/');
        if (parts.Length > 0 && int.TryParse(parts[^1], out var id))
            return id;

        return 0;
    }
}

/// <summary>
/// Attributes for work item relations
/// </summary>
public class RelationAttributes
{
    [JsonPropertyName("comment")]
    public string? Comment { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }
}
