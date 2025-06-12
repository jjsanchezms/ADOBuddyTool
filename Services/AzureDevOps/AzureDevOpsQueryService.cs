using CreateRoadmapADO.Configuration;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace CreateRoadmapADO.Services.AzureDevOps;

/// <summary>
/// Service for Azure DevOps work item queries
/// Focused on retrieving work items through various query patterns
/// </summary>
public class AzureDevOpsQueryService : IAzureDevOpsQueryService, IDisposable
{
    #region Constants

    private const string ApiVersion = "7.0";
    private const string DefaultAreaPath = "SPOOL\\Resource Provider";
    private const string WeeklyDeploymentTasksTag = "WeeklyDeploymentTasks";
    private const string JsonMediaType = "application/json";

    #endregion

    #region Private Fields

    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsQueryService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    #endregion

    #region Constructor

    public AzureDevOpsQueryService(ILogger<AzureDevOpsQueryService> logger)
    {
        _options = ConfigurationReader.GetAzureDevOpsOptions();
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!_options.IsValid())
        {
            throw new InvalidOperationException("Azure DevOps configuration is invalid");
        }

        _httpClient = new HttpClient();
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
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(JsonMediaType));
    }

    #endregion

    #region Public Query Methods

    public async Task<IEnumerable<WorkItem>> GetWorkItemsAsync(CancellationToken cancellationToken = default)
    {
        // Default to the legacy area path for backward compatibility
        return await GetWorkItemsAsync(1000, DefaultAreaPath, cancellationToken);
    }

    public async Task<IEnumerable<WorkItem>> GetWorkItemsAsync(int limit, string areaPath, CancellationToken cancellationToken = default)
    {
        // Default to Feature work items for backward compatibility
        return await GetWorkItemsAsync(limit, areaPath, "Feature", cancellationToken);
    }

    public async Task<IEnumerable<WorkItem>> GetWorkItemsAsync(int limit, string areaPath, string workItemType, CancellationToken cancellationToken = default)
    {
        try
        {
            // WIQL query to retrieve work items ordered by BacklogPriority, filtered by AreaPath, excluding Removed state and KTLO tasks
            var wiqlQuery = $"SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] = '{workItemType}' AND [System.AreaPath] UNDER '{areaPath}' AND [System.State] NOT IN ('Removed','Closed') AND NOT [System.Tags] CONTAINS '{WeeklyDeploymentTasksTag}' ORDER BY [Microsoft.VSTS.Common.StackRank] ASC, [System.Id] ASC";
            var workItemIds = await ExecuteWiqlQueryAsync(wiqlQuery, cancellationToken);

            // Take only the requested number of IDs
            var limitedIds = workItemIds.Take(Math.Min(limit, 1000));

            if (!limitedIds.Any())
            {
                _logger.LogInformation("No {WorkItemType} work items found.", workItemType);
                return Enumerable.Empty<WorkItem>();
            }
            _logger.LogInformation("Found {Count} {WorkItemType} work items, retrieving {Limit}", workItemIds.Count(), workItemType, limitedIds.Count());

            // Get the full work item details for the limited set
            return await GetWorkItemsByIdsAsync(limitedIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving {WorkItemType} work items", workItemType);
            throw;
        }
    }

    public async Task<IEnumerable<WorkItem>> GetWorkItemsForHygieneChecksAsync(int limit, string areaPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // WIQL query to retrieve both Feature and Release Train work items for hygiene checks
            var wiqlQuery = $"SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] IN ('Feature', 'Release Train') AND [System.AreaPath] UNDER '{areaPath}' AND [System.State] NOT IN ('Removed','Closed') ORDER BY [Microsoft.VSTS.Common.StackRank] ASC, [System.Id] ASC";
            var workItemIds = await ExecuteWiqlQueryAsync(wiqlQuery, cancellationToken);

            // Take only the requested number of IDs
            var limitedIds = workItemIds.Take(Math.Min(limit, 1000));

            if (!limitedIds.Any())
            {
                _logger.LogInformation("No Feature or Release Train work items found for hygiene checks.");
                return Enumerable.Empty<WorkItem>();
            }

            _logger.LogInformation("Found {Count} Feature/Release Train work items for hygiene checks, retrieving {Limit}", workItemIds.Count(), limitedIds.Count());

            // Get the full work item details with relations for hygiene checks
            return await GetWorkItemsWithRelationsByIdsAsync(limitedIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work items for hygiene checks");
            throw;
        }
    }

    public async Task<IEnumerable<WorkItem>> GetWorkItemsForSwagUpdatesAsync(int limit, string areaPath, CancellationToken cancellationToken = default)
    {
        try
        {
            // WIQL query to retrieve both Feature and Release Train work items for SWAG updates
            // Include closed Features since completed work should count toward SWAG calculations
            var wiqlQuery = $"SELECT [System.Id] FROM WorkItems WHERE [System.WorkItemType] IN ('Feature', 'Release Train') AND [System.AreaPath] UNDER '{areaPath}' AND [System.State] NOT IN ('Removed') ORDER BY [Microsoft.VSTS.Common.StackRank] ASC, [System.Id] ASC";
            var workItemIds = await ExecuteWiqlQueryAsync(wiqlQuery, cancellationToken);

            // Take only the requested number of IDs
            var limitedIds = workItemIds.Take(Math.Min(limit, 1000));

            if (!limitedIds.Any())
            {
                _logger.LogInformation("No Feature or Release Train work items found for SWAG updates.");
                return Enumerable.Empty<WorkItem>();
            }

            _logger.LogInformation("Found {Count} Feature/Release Train work items for SWAG updates (including closed), retrieving {Limit}", workItemIds.Count(), limitedIds.Count());

            // Get the full work item details with relations for SWAG updates
            return await GetWorkItemsWithRelationsByIdsAsync(limitedIds, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving work items for SWAG updates");
            throw;
        }
    }

    public async Task<WorkItem?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            var fields = "System.Id,System.Title,System.WorkItemType,System.State,System.Description,Skype.StatusNotes,System.IterationPath,System.Tags,Microsoft.VSTS.Common.StackRank,Skype.Swag";
            var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{workItemId}?fields={fields}&api-version={ApiVersion}";

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
            // Include relations by using the $expand parameter (cannot use fields parameter with expand)
            var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{workItemId}?$expand=relations&api-version={ApiVersion}";

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

                        // Check if it's a Release Train with auto-generated tag and is not the current work item
                        if (relatedItem != null &&
                            relatedItem.WorkItemType == "Release Train" &&
                            !string.IsNullOrEmpty(relatedItem.Tags) &&
                            relatedItem.Tags.Split(';', StringSplitOptions.RemoveEmptyEntries)
                                .Any(tag => tag.Trim().Equals("auto-generated", StringComparison.OrdinalIgnoreCase)))
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

    #endregion

    #region Private Helper Methods

    private async Task<IEnumerable<int>> ExecuteWiqlQueryAsync(string wiqlQuery, CancellationToken cancellationToken)
    {
        var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/wiql?api-version={ApiVersion}"; var queryRequest = new { query = wiqlQuery };
        var jsonContent = JsonSerializer.Serialize(queryRequest, _jsonOptions);
        var content = new StringContent(jsonContent, Encoding.UTF8, JsonMediaType);

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
        // Include the fields parameter to ensure we get all necessary fields including IterationPath
        var fields = "System.Id,System.Title,System.WorkItemType,System.State,System.Description,Skype.StatusNotes,System.IterationPath,System.Tags,Microsoft.VSTS.Common.StackRank,Skype.Swag";
        var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems?ids={ids}&fields={fields}&api-version={ApiVersion}";

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

        // Convert work items to dictionary for quick lookup
        var workItemsDict = workItemsResponse?.Value?
            .Select(ConvertToWorkItem)
            .ToDictionary(wi => wi.Id, wi => wi) ?? new Dictionary<int, WorkItem>();

        // Return work items in the same order as the input IDs
        return workItemIds
            .Where(id => workItemsDict.ContainsKey(id))
            .Select(id => workItemsDict[id]);
    }

    private async Task<IEnumerable<WorkItem>> GetWorkItemsWithRelationsByIdsAsync(IEnumerable<int> workItemIds, CancellationToken cancellationToken)
    {
        // For hygiene checks, we need to get each work item individually with relations
        // since the batch API doesn't support expanding relations for multiple items
        var workItems = new List<WorkItem>();

        foreach (var id in workItemIds)
        {
            var workItem = await GetWorkItemWithRelationsAsync(id, cancellationToken);
            if (workItem != null)
            {
                workItems.Add(workItem);
            }
        }
        return workItems;
    }

    #endregion

    #region Data Conversion Methods

    private WorkItem ConvertToWorkItem(WorkItemResponse? response)
    {
        if (response?.Fields == null)
            return new WorkItem();

        // Debugging output to see what's coming from ADO for StackRank
        var stackRankValue = GetFieldValue(response.Fields, "Microsoft.VSTS.Common.StackRank"); double? stackRank = null;
        if (stackRankValue != null)
        {
            if (double.TryParse(stackRankValue, out var stackRankDouble))
            {
                stackRank = stackRankDouble;
            }
        }
        return new WorkItem
        {
            Id = response.Id,
            Title = GetFieldValue(response.Fields, "System.Title") ?? string.Empty,
            WorkItemType = GetFieldValue(response.Fields, "System.WorkItemType") ?? string.Empty,
            State = GetFieldValue(response.Fields, "System.State") ?? string.Empty,
            Description = GetFieldValue(response.Fields, "System.Description") ?? string.Empty,
            StatusNotes = GetFieldValue(response.Fields, "Skype.StatusNotes") ?? string.Empty,
            IterationPath = GetFieldValue(response.Fields, "System.IterationPath"),
            Tags = GetFieldValue(response.Fields, "System.Tags") ?? string.Empty,
            StackRank = stackRank,
            Swag = GetSwagValue(response.Fields),
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

    private static double? GetSwagValue(Dictionary<string, JsonElement> fields)
    {
        var swagValue = GetFieldValue(fields, "Skype.Swag");
        if (swagValue != null && double.TryParse(swagValue, out var swag))
        {
            return swag;
        }
        return null;
    }

    #endregion

    #region Resource Management

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #endregion
}

#region Internal Response Models

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

#endregion
