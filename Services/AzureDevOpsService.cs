using CreateRoadmapADO.Configuration;
using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Service for interacting with Azure DevOps API
/// </summary>
public class AzureDevOpsService : IAzureDevOpsService
{
    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public AzureDevOpsService(
        HttpClient httpClient,
        IOptions<AzureDevOpsOptions> options,
        ILogger<AzureDevOpsService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_options.IsValid())
        {
            throw new InvalidOperationException("Azure DevOps configuration is invalid");
        }

        ConfigureHttpClient();
        
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }

    private void ConfigureHttpClient()
    {
        var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($":{_options.PersonalAccessToken}"));
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<IEnumerable<WorkItem>> GetWorkItemsAsync(CancellationToken cancellationToken = default)
    {
        return await GetWorkItemsAsync(1000, cancellationToken);
    }

    public async Task<IEnumerable<WorkItem>> GetWorkItemsAsync(int limit, CancellationToken cancellationToken = default)
    {
        try
        {
            const string workItemType = "Feature"; // Hardcoded to only retrieve Features

            // WIQL query to retrieve work items ordered by BacklogPriority, filtered by AreaPath and excluding Removed state
            var wiqlQuery = $"SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = '{workItemType}' AND [System.AreaPath] UNDER 'SPOOL\\Resource Provider' AND [System.State] NOT IN ('Removed','Closed') ORDER BY [Microsoft.VSTS.Common.StackRank] ASC, [System.Id] ASC";
            var workItemIds = await ExecuteWiqlQueryAsync(wiqlQuery, cancellationToken);

            // Take only the requested number of IDs
            var limitedIds = workItemIds.Take(Math.Min(limit, 1000));

            if (!limitedIds.Any())
            {
                _logger.LogInformation("No Feature work items found.");
                return Enumerable.Empty<WorkItem>();
            }

            _logger.LogInformation("Found {Count} Feature work items, retrieving {Limit}", workItemIds.Count(), limitedIds.Count());

            // Get the full work item details for the limited set
            return await GetWorkItemsByIdsAsync(limitedIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving Feature work items");
            throw;
        }
    }

    public async Task<WorkItem?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{workItemId}?api-version=7.0";
            
            _logger.LogDebug("Making request to: {Url}", url);
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to retrieve work item {WorkItemId}. Status: {StatusCode}, Content: {Content}", workItemId, response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var workItemResponse = JsonSerializer.Deserialize<WorkItemResponse>(content, _jsonOptions);
            
            return ConvertToWorkItem(workItemResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work item {WorkItemId}", workItemId);
            throw;
        }
    }

    public async Task<WorkItem?> GetWorkItemWithRelationsAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Include relations by using the $expand parameter
            var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{workItemId}?$expand=relations&api-version=7.0";
            
            _logger.LogDebug("Making request to get work item with relations: {Url}", url);
            
            var response = await _httpClient.GetAsync(url, cancellationToken);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Failed to retrieve work item with relations {WorkItemId}. Status: {StatusCode}, Content: {Content}", 
                    workItemId, response.StatusCode, errorContent);
                return null;
            }

            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            var workItemResponse = JsonSerializer.Deserialize<WorkItemResponse>(content, _jsonOptions);
            
            return ConvertToWorkItemWithRelations(workItemResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work item with relations {WorkItemId}", workItemId);
            throw;
        }
    }
    
    public async Task<int> GetExistingRelatedParentItemIdAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            // Get the work item with its relations
            var workItem = await GetWorkItemWithRelationsAsync(workItemId, cancellationToken);
            
            if (workItem == null || !workItem.Relations.Any())
                return 0;
                
            // Look for related items
            foreach (var relation in workItem.Relations)
            {
                // Check only "Related" links 
                if (relation.Rel == "System.LinkTypes.Related")
                {
                    var relatedId = relation.GetRelatedWorkItemId();
                    if (relatedId > 0)
                    {
                        // Get the related work item
                        var relatedItem = await GetWorkItemByIdAsync(relatedId, cancellationToken);
                        
                        // Check if it's an Epic with auto-generated tag and is not the current work item
                        if (relatedItem != null && 
                            relatedItem.WorkItemType == "Epic" &&
                            relatedItem.Tags.Contains("auto-generated"))
                        {
                            _logger.LogInformation("Found existing related auto-generated parent: #{RelatedId} ({Title})", 
                                relatedId, relatedItem.Title);
                            return relatedId;
                        }
                    }
                }
            }
            
            // No matching related parent found
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing related parent for work item {WorkItemId}", workItemId);
            return 0;
        }
    }

    public async Task<IEnumerable<WorkItem>> GetWorkItemsByQueryAsync(string wiqlQuery, CancellationToken cancellationToken = default)
    {
        try
        {
            var workItemIds = await ExecuteWiqlQueryAsync(wiqlQuery, cancellationToken);
            
            if (!workItemIds.Any())
            {
                _logger.LogInformation("No work items found for the query");
                return Enumerable.Empty<WorkItem>();
            }

            return await GetWorkItemsByIdsAsync(workItemIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error executing WIQL query: {Query}", wiqlQuery);
            throw;
        }
    }

    public async Task<int> CheckForExistingParentAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Checking for existing parent for work item #{WorkItemId}", workItemId);
            
            // Build the WIQL query to find Epic work items that have a Related link to this work item
            // and also have the auto-generated tag
            var wiqlQuery = $@"
                SELECT [System.Id]
                FROM WorkItems 
                WHERE [System.WorkItemType] = 'Epic' 
                AND [System.Tags] CONTAINS 'auto-generated'
                AND [System.Id] IN (
                    SELECT [System.Id] 
                    FROM WorkItemLinks 
                    WHERE [Source].[System.Id] = {workItemId}
                    AND [System.Links.LinkType] = 'System.LinkTypes.Related'
                    MODE (MayContain)
                )
            ";
            
            var results = await GetWorkItemsByQueryAsync(wiqlQuery, cancellationToken);
            var parent = results.FirstOrDefault();
            
            if (parent != null)
            {
                _logger.LogInformation("Found existing parent item #{ParentId} with title: {Title}", parent.Id, parent.Title);
                return parent.Id;
            }
            
            _logger.LogInformation("No existing parent found for work item #{WorkItemId}", workItemId);
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing parent item for work item #{WorkItemId}", workItemId);
            return 0; // Return 0 instead of throwing to allow the process to continue
        }
    }

    public async Task CreateEpicAsync(List<int> children, string title, int patternItemId = 0)
    {
        _logger.LogInformation("Creating Epic with title: {Title}", title);
        _logger.LogInformation("Child items: {ChildrenCount}", children.Count);
        Console.WriteLine($"Creating Epic: {title}");
        Console.WriteLine($"With {children.Count} children: {string.Join(", ", children)}");

        try
        {
            // Create the Epic work item
            var epicId = await CreateWorkItemAsync("Epic", title);
            
            if (epicId > 0)
            {
                // Create relations to child work items
                await CreateRelationsAsync(epicId, children);
                
                // If we have a pattern item ID, also create a specific relation to that item
                if (patternItemId > 0 && !children.Contains(patternItemId))
                {
                    await CreateRelationAsync(epicId, patternItemId, "Auto-generated from pattern item");
                }
                
                _logger.LogInformation("Successfully created Epic #{EpicId}: {Title}", epicId, title);
                Console.WriteLine($"Successfully created Epic #{epicId}: {title}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Epic: {Title}", title);
            Console.WriteLine($"Error creating Epic: {ex.Message}");
            throw;
        }
    }
    
    public async Task CreateReleaseTrainAsync(List<int> children, string title, int patternItemId = 0)
    {
        _logger.LogInformation("Creating Release Train with title: {Title}, patternItemId: {PatternItemId}", title, patternItemId);
        _logger.LogInformation("Child items: {ChildrenCount}", children.Count);
        Console.WriteLine($"Creating Release Train: {title}");
        Console.WriteLine($"With {children.Count} children: {string.Join(", ", children)}");

        try
        {
            // Create the Release Train as an Epic (assuming Release Train is not a native work item type)
            var releaseTrainId = await CreateWorkItemAsync("Epic", $"RELEASE TRAIN: {title}");
            
            if (releaseTrainId > 0)
            {
                // Create relations to child work items
                await CreateRelationsAsync(releaseTrainId, children);
                
                // If we have a pattern item ID, also create a specific relation to that item
                if (patternItemId > 0 && !children.Contains(patternItemId))
                {
                    await CreateRelationAsync(releaseTrainId, patternItemId, "Auto-generated from pattern item");
                }
                
                _logger.LogInformation("Successfully created Release Train #{ReleaseTrainId}: {Title}", releaseTrainId, title);
                Console.WriteLine($"Successfully created Release Train #{releaseTrainId}: {title}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Release Train: {Title}", title);
            Console.WriteLine($"Error creating Release Train: {ex.Message}");
            throw;
        }
    }
    
    public async Task CreateRelationAsync(int sourceId, int targetId, string comment = "")
    {
        try
        {
            _logger.LogInformation("Creating relation from #{SourceId} to #{TargetId}", sourceId, targetId);
            
            var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{sourceId}?api-version=7.0";
            
            var patchOperation = new[]
            {
                new 
                {
                    op = "add",
                    path = "/relations/-",
                    value = new
                    {
                        rel = "System.LinkTypes.Related",
                        url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{targetId}",
                        attributes = new
                        {
                            comment = string.IsNullOrEmpty(comment) ? "Auto-generated relation" : comment
                        }
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(patchOperation, _jsonOptions);
            var content = new StringContent(jsonContent, Encoding.UTF8, "application/json-patch+json");
            
            var response = await _httpClient.PatchAsync(url, content);
            
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to create relation from #{SourceId} to #{TargetId}. Status: {StatusCode}, Content: {Content}", 
                    sourceId, targetId, response.StatusCode, errorContent);
                throw new HttpRequestException($"Failed to create relation with status {response.StatusCode}: {errorContent}");
            }
            
            _logger.LogInformation("Successfully created relation from #{SourceId} to #{TargetId}", sourceId, targetId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating relation from #{SourceId} to #{TargetId}", sourceId, targetId);
            throw;
        }
    }

    private async Task<IEnumerable<int>> ExecuteWiqlQueryAsync(string wiqlQuery, CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/wiql?api-version=7.0";
        
        var queryRequest = new { query = wiqlQuery };
        var jsonContent = JsonSerializer.Serialize(queryRequest, _jsonOptions);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

        // Print the full WIQL query to the console for debugging
        Console.WriteLine("************************************************************");
        Console.WriteLine($"EXECUTING WIQL QUERY: {wiqlQuery}");
        Console.WriteLine($"URL: {url}");
        Console.WriteLine("************************************************************");
        
        _logger.LogInformation("Executing WIQL query: {Query}", wiqlQuery);
        _logger.LogDebug("Making request to URL: {Url}", url);
        
        var response = await _httpClient.PostAsync(url, content, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("WIQL query failed. Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
            throw new HttpRequestException($"WIQL query failed with status {response.StatusCode}: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        var queryResult = JsonSerializer.Deserialize<WiqlQueryResult>(responseContent, _jsonOptions);

        return queryResult?.WorkItems?.Select(wi => wi.Id) ?? Enumerable.Empty<int>();
    }

    private async Task<IEnumerable<WorkItem>> GetWorkItemsByIdsAsync(IEnumerable<int> workItemIds, CancellationToken cancellationToken)
    {
        var ids = string.Join(",", workItemIds);
        var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems?ids={ids}&api-version=7.0";
        
        _logger.LogDebug("Retrieving work items by IDs: {Ids}", ids);
        _logger.LogDebug("Making request to URL: {Url}", url);
        
        var response = await _httpClient.GetAsync(url, cancellationToken);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError("Failed to retrieve work items. Status: {StatusCode}, Content: {Content}", response.StatusCode, errorContent);
            throw new HttpRequestException($"Failed to retrieve work items with status {response.StatusCode}: {errorContent}");
        }

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        var workItemsResponse = JsonSerializer.Deserialize<WorkItemsResponse>(content, _jsonOptions);

        return workItemsResponse?.Value?.Select(ConvertToWorkItem) ?? Enumerable.Empty<WorkItem>();
    }

    private WorkItem ConvertToWorkItem(WorkItemResponse? response)
    {
        if (response?.Fields == null)
            return new WorkItem();

        // Debugging output to see what's coming from ADO for StackRank
        var stackRankValue = GetFieldValue(response.Fields, "Microsoft.VSTS.Common.StackRank");
        
        double? stackRank = null;
        if (stackRankValue != null)
        {
            if (double.TryParse(stackRankValue, out var stackRankDouble))
            {
                stackRank = stackRankDouble;
            }
            else
            {
                Console.WriteLine($"DEBUG: Failed to parse StackRank value: '{stackRankValue}'");
            }
        }
        
        return new WorkItem
        {
            Id = response.Id,
            Title = GetFieldValue(response.Fields, "System.Title") ?? string.Empty,
            WorkItemType = GetFieldValue(response.Fields, "System.WorkItemType") ?? string.Empty,
            State = GetFieldValue(response.Fields, "System.State") ?? string.Empty,
            Description = GetFieldValue(response.Fields, "System.Description") ?? string.Empty,
            Tags = GetFieldValue(response.Fields, "System.Tags") ?? string.Empty,
            StackRank = stackRank,
            Relations = new List<WorkItemRelation>() // Empty relations list since we didn't request them
        };
    }

    private WorkItem ConvertToWorkItemWithRelations(WorkItemResponse? response)
    {
        // Get the base work item without relations
        var workItem = ConvertToWorkItem(response);
        
        // Now add the relations if available
        if (response?.Relations != null)
        {
            workItem.Relations = response.Relations
                .Select(r => new WorkItemRelation
                {
                    Rel = r.Rel ?? string.Empty,
                    Url = r.Url ?? string.Empty,
                    Attributes = r.Attributes != null ? new RelationAttributes
                    {
                        Comment = r.Attributes.Value.TryGetProperty("comment", out var comment) ? comment.GetString() : null,
                        Name = r.Attributes.Value.TryGetProperty("name", out var name) ? name.GetString() : null
                    } : null
                })
                .ToList();
        }
        
        return workItem;
    }

    private static string? GetFieldValue(Dictionary<string, JsonElement> fields, string key)
    {
        if (fields.TryGetValue(key, out var element))
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                return element.GetString();
            }
            else if (element.ValueKind == JsonValueKind.Number)
            {
                // Handle numeric values by converting them to string
                if (element.TryGetDouble(out var doubleValue))
                {
                    return doubleValue.ToString();
                }
                else if (element.TryGetInt32(out var intValue))
                {
                    return intValue.ToString();
                }
            }
            else if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty("displayName", out var displayName))
            {
                return displayName.GetString();
            }
        }
        return null;
    }

    private async Task<int> CreateWorkItemAsync(string workItemType, string title)
    {
        var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/${workItemType}?api-version=7.0";
        
        _logger.LogDebug($"Creating {workItemType} with title: {title}");
        _logger.LogDebug($"Making request to URL: {url}");
        
        // Create document with the fields for the new work item
        var patchDocument = new[]
        {
            new { op = "add", path = "/fields/System.Title", value = title },
            new { op = "add", path = "/fields/System.AreaPath", value = "SPOOL\\Resource Provider" },
            new { op = "add", path = "/fields/System.Tags", value = "auto-generated" },
            new { op = "add", path = "/fields/System.Description", value = $"Auto-generated {workItemType} created on {DateTime.Now:yyyy-MM-dd HH:mm:ss}" }
        };

        var jsonContent = JsonSerializer.Serialize(patchDocument, _jsonOptions);
        
        // ADO requires a specific content type for PATCH operations
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json-patch+json");
        
        var response = await _httpClient.PatchAsync(url, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to create {workItemType}. Status: {response.StatusCode}, Content: {errorContent}");
            throw new HttpRequestException($"Failed to create {workItemType} with status {response.StatusCode}: {errorContent}");
        }

        var responseContent = await response.Content.ReadAsStringAsync();
        var workItem = JsonSerializer.Deserialize<WorkItemResponse>(responseContent, _jsonOptions);
        
        return workItem?.Id ?? 0;
    }
    
    private async Task CreateRelationsAsync(int parentId, List<int> childrenIds)
    {
        if (childrenIds == null || !childrenIds.Any())
        {
            return;
        }
        
        var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{parentId}?api-version=7.0";
        
        _logger.LogDebug($"Creating relations for work item #{parentId} to {childrenIds.Count} children");
        
        // Add a relation for each child
        var patchOperations = childrenIds.Select(childId => new
        {
            op = "add",
            path = "/relations/-",
            value = new
            {
                rel = "System.LinkTypes.Related",
                url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{childId}",
                attributes = new
                {
                    comment = "Auto-generated relation"
                }
            }
        }).ToList();

        var jsonContent = JsonSerializer.Serialize(patchOperations, _jsonOptions);
        var content = new StringContent(jsonContent, Encoding.UTF8, "application/json-patch+json");
        
        var response = await _httpClient.PatchAsync(url, content);
        
        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync();
            _logger.LogError($"Failed to create relations for work item #{parentId}. Status: {response.StatusCode}, Content: {errorContent}");
            throw new HttpRequestException($"Failed to create relations for work item #{parentId} with status {response.StatusCode}: {errorContent}");
        }
        
        _logger.LogInformation($"Successfully created {childrenIds.Count} relations for work item #{parentId}");
    }
}

// Internal response models for Azure DevOps API
internal class WiqlQueryResult
{
    public List<WorkItemReference>? WorkItems { get; set; }
}

internal class WorkItemReference
{
    public int Id { get; set; }
}

internal class WorkItemsResponse
{
    public List<WorkItemResponse>? Value { get; set; }
}

internal class WorkItemResponse
{
    public int Id { get; set; }
    public Dictionary<string, JsonElement>? Fields { get; set; }
    public string? Url { get; set; }
    public List<WorkItemRelationResponse>? Relations { get; set; }
}

internal class WorkItemRelationResponse
{
    public string? Rel { get; set; }
    public string? Url { get; set; }
    public JsonElement? Attributes { get; set; }
}
