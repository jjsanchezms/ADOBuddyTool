using ADOBuddyTool.Presentation.Configuration;
using Microsoft.Extensions.Logging;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace ADOBuddyTool.Infrastructure.AzureDevOps.Services;

/// <summary>
/// Service for Azure DevOps work item relationships
/// Focused on creating and managing work item relations
/// </summary>
public class AzureDevOpsRelationService : IAzureDevOpsRelationService, IDisposable
{
    #region Constants

    private const string ApiVersion = "7.0";
    private const string JsonPatchMediaType = "application/json-patch+json";
    private const string JsonMediaType = "application/json";

    #endregion

    #region Private Fields

    private readonly HttpClient _httpClient;
    private readonly AzureDevOpsOptions _options;
    private readonly ILogger<AzureDevOpsRelationService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    #endregion

    #region Constructor

    public AzureDevOpsRelationService(ILogger<AzureDevOpsRelationService> logger)
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

    #region Public Methods

    public async Task CreateRelationAsync(int sourceId, int targetId, string comment = "")
    {
        try
        {
            _logger.LogInformation("Creating relation from #{SourceId} to #{TargetId}", sourceId, targetId);

            var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{sourceId}?api-version={ApiVersion}";

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
            var content = new StringContent(jsonContent, Encoding.UTF8, JsonPatchMediaType);

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

    /// <summary>
    /// Checks if a work item has a related auto-generated Release Train
    /// </summary>
    /// <param name="workItemId">Work item ID to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The ID of the existing related item, or 0 if none exists</returns>
    public async Task<int> GetExistingRelatedParentItemIdAsync(int workItemId, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Checking for existing related parent for work item #{WorkItemId}", workItemId);

            // Get work item with relations
            var url = $"{_options.BaseUrl}/{_options.Project}/_apis/wit/workitems/{workItemId}?$expand=Relations&api-version={ApiVersion}";

            var response = await _httpClient.GetAsync(url, cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to get work item #{WorkItemId} with status {StatusCode}", workItemId, response.StatusCode);
                return 0;
            }

            var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken);
            var jsonDoc = JsonDocument.Parse(jsonContent);

            if (!jsonDoc.RootElement.TryGetProperty("relations", out var relationsElement))
            {
                return 0;
            }

            // Look for existing auto-generated relations
            foreach (var relation in relationsElement.EnumerateArray())
            {
                if (relation.TryGetProperty("rel", out var relProperty) &&
                    relProperty.GetString() == "System.LinkTypes.Related" &&
                    relation.TryGetProperty("attributes", out var attributesProperty) &&
                    attributesProperty.TryGetProperty("comment", out var commentProperty))
                {
                    var comment = commentProperty.GetString();
                    if (!string.IsNullOrEmpty(comment) && comment.Contains("auto-generated", StringComparison.OrdinalIgnoreCase))
                    {
                        if (relation.TryGetProperty("url", out var urlProperty))
                        {
                            var relatedUrl = urlProperty.GetString();
                            if (!string.IsNullOrEmpty(relatedUrl))
                            {
                                // Extract work item ID from URL
                                var segments = relatedUrl.Split('/');
                                if (segments.Length > 0 && int.TryParse(segments[^1], out var relatedId))
                                {
                                    _logger.LogDebug("Found existing related parent #{RelatedId} for work item #{WorkItemId}", relatedId, workItemId);
                                    return relatedId;
                                }
                            }
                        }
                    }
                }
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking for existing related parent for work item #{WorkItemId}", workItemId);
            return 0;
        }
    }

    #endregion

    #region Resource Management

    public void Dispose()
    {
        _httpClient?.Dispose();
    }

    #endregion
}

