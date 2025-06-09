using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Service for generating and managing roadmaps from work items
/// </summary>
public class RoadmapService : IRoadmapService
{
    private readonly ILogger<RoadmapService> _logger;
    private readonly IAzureDevOpsService _azureDevOpsService;

    public RoadmapService(ILogger<RoadmapService> logger, IAzureDevOpsService azureDevOpsService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _azureDevOpsService = azureDevOpsService ?? throw new ArgumentNullException(nameof(azureDevOpsService));
    }

    public async Task<IEnumerable<RoadmapItem>> GenerateRoadmapAsync(IEnumerable<WorkItem> workItems, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Generating roadmap from {Count} work items", workItems.Count());

            // Process special titles to create epics/release trains
            await ProcessSpecialTitlesAsync(workItems);
            
            var roadmapItems = workItems.Select(ConvertToRoadmapItem).ToList();
            
            // Sort by priority and dependencies
            var sortedItems = SortRoadmapItems(roadmapItems);

            _logger.LogInformation("Successfully generated roadmap with {Count} items", sortedItems.Count());
            
            return await Task.FromResult(sortedItems);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating roadmap from work items");
            throw;
        }
    }    /// <summary>
    /// Processes work items to identify special title patterns and create epics/release trains
    /// </summary>
    /// <param name="workItems">Collection of work items</param>
    /// <returns>Task</returns>
    private async Task ProcessSpecialTitlesAsync(IEnumerable<WorkItem> workItems)
    {
        var workItemsList = workItems.ToList();
        if (!workItemsList.Any())
            return;

        List<int> currentChildren = new();
        string? currentTitle = null;
        bool isCollectingItems = false;
        bool isReleaseTrain = false;

        _logger.LogInformation("Scanning for special title patterns in work items");        foreach (var workItem in workItemsList)
        {
            // Check for special title patterns
            // Match titles like "----- TITLE -----rt" or "----- TITLE -----e"
            // with any number of dashes and spaces
            var patternStart = new Regex(@"^-+\s*(.*?)\s*-+(?:rt|e)$");
            var match = patternStart.Match(workItem.Title);
            
            if (match.Success)
            {
                // If we were already collecting items, create the previous epic/release train
                if (isCollectingItems && currentTitle != null && currentChildren.Any())
                {
                    _logger.LogInformation("Creating previous group before starting new one");
                    await CreateItemFromPattern(currentChildren, currentTitle, isReleaseTrain);
                    currentChildren.Clear();
                }

                // Extract new title from between the markers
                currentTitle = match.Groups[1].Value.Trim();
                isReleaseTrain = workItem.Title.EndsWith("rt");
                isCollectingItems = true;
                
                _logger.LogInformation("Found {Type} pattern: \"{Title}\"", 
                    isReleaseTrain ? "Release Train" : "Epic", currentTitle);
            }            // If we're collecting items and this isn't a special title, add it to current children
            else if (isCollectingItems)
            {
                // Check if this is an end marker without a new group (just dashes)
                // Match patterns that start and end with at least 3 dashes and don't contain "rt" or "e" at the end
                var isEndMarker = workItem.Title.StartsWith("---") && workItem.Title.EndsWith("---") && 
                    !workItem.Title.EndsWith("rt") && !workItem.Title.EndsWith("e");
                
                if (isEndMarker)
                {
                    _logger.LogInformation("Found end marker: {Title}", workItem.Title);
                    
                    // End current group
                    if (currentTitle != null && currentChildren.Any())
                    {
                        await CreateItemFromPattern(currentChildren, currentTitle, isReleaseTrain);
                        currentChildren.Clear();
                        isCollectingItems = false;
                    }
                }
                else
                {
                    // Add to current children
                    currentChildren.Add(workItem.Id);
                    _logger.LogDebug("Added child work item: {Id} - {Title}", workItem.Id, workItem.Title);
                }
            }
        }

        // Don't forget to create the last epic/release train if we were collecting items
        if (isCollectingItems && currentTitle != null && currentChildren.Any())
        {
            _logger.LogInformation("Creating final group at end of processing");
            await CreateItemFromPattern(currentChildren, currentTitle, isReleaseTrain);
        }
    }    /// <summary>
    /// Creates an epic or release train based on collected children and pattern
    /// <summary>
    /// Creates an Epic or Release Train item from pattern items
    /// </summary>
    private async Task CreateItemFromPattern(List<int> children, string title, bool isReleaseTrain)
    {
        // Create a divider for better console readability
        Console.WriteLine(new string('=', 80));
        
        // Get the pattern item which should be the item with the matching pattern
        int patternItemId = children.FirstOrDefault();
        bool shouldCreateNew = true;
        
        if (patternItemId > 0)
        {
            // Get the pattern item to check its relations
            var patternItem = await _azureDevOpsService.GetWorkItemByIdAsync(patternItemId);
              if (patternItem != null)
            {                // Use a simpler approach: query for Epics and Release Trains with auto-generated tag first
                // Then filter by checking relations in code rather than complex WIQL
                var wiqlQuery = $"SELECT [System.Id] FROM WorkItems " +
                               $"WHERE [System.WorkItemType] IN ('Epic', 'Release Train') " +
                               $"AND [System.Tags] CONTAINS 'auto-generated'";
                               
                var candidateItems = await _azureDevOpsService.GetWorkItemsByQueryAsync(wiqlQuery);
                
                // Now check each candidate to see if it's related to our pattern item
                var relatedItems = new List<WorkItem>();
                foreach (var candidate in candidateItems)
                {
                    try
                    {                        // Get the full work item with relations
                        var fullCandidate = await _azureDevOpsService.GetWorkItemByIdAsync(candidate.Id);
                        if (fullCandidate?.Relations != null)
                        {
                            // Check if this candidate is related to our pattern item
                            var isRelated = fullCandidate.Relations.Any(r => 
                                r.Rel?.Contains("Related") == true && 
                                r.GetRelatedWorkItemId() == patternItemId);
                                
                            if (isRelated)
                            {
                                relatedItems.Add(candidate);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Error checking relations for work item {Id}", candidate.Id);
                        // Continue with other candidates
                    }                }
                  var existingParent = relatedItems.FirstOrDefault(wi => 
                    (wi.WorkItemType == "Epic" || wi.WorkItemType == "Release Train") && wi.Tags.Contains("auto-generated"));
                
                if (existingParent != null)
                {                    // We found an existing parent, so we shouldn't create a new one
                    var parentType = existingParent.WorkItemType == "Release Train" ? "RELEASE TRAIN" : "EPIC";
                    Console.WriteLine($"FOUND EXISTING {parentType}: " +
                                     $"#{existingParent.Id} - {existingParent.Title}");
                    _logger.LogInformation("Found existing {Type} #{Id} - {Title}",
                        parentType, 
                        existingParent.Id, existingParent.Title);
                    
                    shouldCreateNew = false;
                }
            }
        }
          if (shouldCreateNew)
        {
            // Create new parent
            if (isReleaseTrain)
            {
                Console.WriteLine($"CREATING RELEASE TRAIN: \"{title}\"");
                Console.WriteLine($"WITH {children.Count} CHILDREN: {string.Join(", ", children)}");
                _logger.LogInformation("Creating Release Train: {Title} with {Count} children", title, children.Count);
                await _azureDevOpsService.CreateReleaseTrainAsync(new List<int>(children), title, patternItemId);
            }
            else
            {
                Console.WriteLine($"CREATING EPIC: \"{title}\"");
                Console.WriteLine($"WITH {children.Count} CHILDREN: {string.Join(", ", children)}");
                _logger.LogInformation("Creating Epic: {Title} with {Count} children", title, children.Count);
                await _azureDevOpsService.CreateEpicAsync(new List<int>(children), title, patternItemId);
            }
        }
        
        Console.WriteLine(new string('=', 80));
        
        // Short delay to ensure logs are readable and any API rate limits are respected
        await Task.Delay(100);
    }

    public RoadmapItem ConvertToRoadmapItem(WorkItem workItem)
    {
        // Set start date to current date for estimation purposes
        var startDate = DateTime.Today;
        
        return new RoadmapItem
        {
            Id = workItem.Id,
            Title = workItem.Title,
            Description = $"Work Item: {workItem.WorkItemType} - {workItem.Title}",
            Type = MapWorkItemTypeToRoadmapType(workItem.WorkItemType),
            Status = string.IsNullOrEmpty(workItem.State) ? 
                RoadmapItemStatus.NotStarted : 
                MapWorkItemStateToRoadmapStatus(workItem.State),
            StackRank = workItem.StackRank,
            // Assign priority based on StackRank if available
            Priority = workItem.StackRank.HasValue ? 
                (int)Math.Ceiling(100 - (workItem.StackRank.Value % 100)) : 
                null,
            // Assign start and end dates for timeline visualization
            StartDate = startDate,
            EndDate = EstimateEndDate(startDate, workItem.WorkItemType)
        };
    }

    public IEnumerable<RoadmapItem> SortRoadmapItems(IEnumerable<RoadmapItem> roadmapItems)
    {
        return roadmapItems            .OrderBy(item => item.StackRank ?? double.MaxValue) // Sort by StackRank (ASC) first
            .ThenByDescending(item => item.Priority ?? 0) // Then by priority (DESC)
            .ThenBy(item => item.StartDate ?? DateTime.MaxValue) // Then by start date (ASC)
            .ThenBy(item => item.Title); // Finally alphabetical
    }

    public async Task CreateEpicAsync(List<int> children, string title)
    {
        // Call the service that actually implements this 
        // to avoid circular dependency issues
        if (_azureDevOpsService != null)
        {
            await _azureDevOpsService.CreateEpicAsync(children, title);
        }
    }
    
    public async Task CreateReleaseTrainAsync(List<int> children, string title)
    {
        // Call the service that actually implements this
        // to avoid circular dependency issues
        if (_azureDevOpsService != null)
        {
            await _azureDevOpsService.CreateReleaseTrainAsync(children, title);
        }
    }

    private static RoadmapItemType MapWorkItemTypeToRoadmapType(string workItemType)
    {
        return workItemType.ToLowerInvariant() switch
        {
            "epic" => RoadmapItemType.Epic,
            "feature" => RoadmapItemType.Feature,
            "initiative" => RoadmapItemType.Initiative,
            _ => RoadmapItemType.Feature
        };
    }

    private static RoadmapItemStatus MapWorkItemStateToRoadmapStatus(string state)
    {
        return state.ToLowerInvariant() switch
        {
            "new" or "proposed" or "approved" => RoadmapItemStatus.NotStarted,
            "active" or "committed" or "in progress" => RoadmapItemStatus.InProgress,
            "done" or "closed" or "completed" => RoadmapItemStatus.Completed,
            "removed" or "cut" => RoadmapItemStatus.Cancelled,
            _ => RoadmapItemStatus.NotStarted
        };
    }

    private static DateTime EstimateEndDate(DateTime startDate, string workItemType)
    {
        // Simple estimation logic - could be enhanced with actual data
        var estimatedDays = workItemType.ToLowerInvariant() switch
        {
            "epic" => 90, // 3 months
            "feature" => 30, // 1 month
            "initiative" => 180, // 6 months
            _ => 14 // 2 weeks
        };

        return startDate.AddDays(estimatedDays);
    }
}
