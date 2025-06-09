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
    
    // Track epic/release train operations for summary
    public EpicReleaseTrainSummary OperationsSummary { get; private set; } = new();

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
            
            // Reset summary for this operation
            OperationsSummary = new EpicReleaseTrainSummary
            {
                TotalBacklogItemsProcessed = workItems.Count(),
                BacklogReadSuccessfully = true
            };

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
            OperationsSummary.BacklogReadSuccessfully = false;
            throw;
        }
    }

    /// <summary>
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
        int? currentExistingId = null;
        bool isCollectingItems = false;
        bool isReleaseTrain = false;

        _logger.LogInformation("Scanning for special title patterns in work items");

        foreach (var workItem in workItemsList)
        {
            // Check for special title patterns
            // Match titles like "----- TITLE -----rt" or "----- TITLE -----e" or with IDs like ":1234"
            // Pattern: ^-+\s*(.*?)\s*-+(?:rt|e)(?::(\d+))?$
            var patternStart = new Regex(@"^-+\s*(.*?)\s*-+(?:rt|e)(?::(\d+))?$");
            var match = patternStart.Match(workItem.Title);
            
            if (match.Success)
            {
                // If we were already collecting items, create the previous epic/release train
                if (isCollectingItems && currentTitle != null && currentChildren.Any())
                {
                    _logger.LogInformation("Creating previous group before starting new one");
                    await CreateItemFromPattern(currentChildren, currentTitle, isReleaseTrain, currentExistingId);
                    currentChildren.Clear();
                }

                // Extract new title from between the markers
                currentTitle = match.Groups[1].Value.Trim();
                
                // Extract existing work item ID if present
                currentExistingId = null;
                if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    if (int.TryParse(match.Groups[2].Value, out int parsedId))
                    {
                        currentExistingId = parsedId;
                    }
                }
                
                isReleaseTrain = workItem.Title.Contains("rt");
                isCollectingItems = true;
                
                _logger.LogInformation("Found {Type} pattern: \"{Title}\"{IdInfo}", 
                    isReleaseTrain ? "Release Train" : "Epic", currentTitle,
                    currentExistingId.HasValue ? $" (ID: {currentExistingId})" : "");
            }
            // If we're collecting items and this isn't a special title, add it to current children
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
                        await CreateItemFromPattern(currentChildren, currentTitle, isReleaseTrain, currentExistingId);
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
            await CreateItemFromPattern(currentChildren, currentTitle, isReleaseTrain, currentExistingId);
        }
    }

    /// <summary>
    /// Creates an Epic or Release Train item from pattern items
    /// </summary>
    private async Task CreateItemFromPattern(List<int> children, string title, bool isReleaseTrain, int? existingWorkItemId = null)
    {
        // Create a divider for better console readability
        Console.WriteLine(new string('=', 80));
        
        // Get the pattern item which should be the item with the matching pattern
        int patternItemId = children.FirstOrDefault();
        
        if (existingWorkItemId.HasValue)
        {
            // Update existing epic/release train by adding missing children
            await UpdateExistingWorkItemWithChildren(existingWorkItemId.Value, children, title, isReleaseTrain);
        }
        else
        {
            // Create new epic/release train
            int newWorkItemId = await CreateNewWorkItemFromPattern(children, title, isReleaseTrain, patternItemId);
            
            // Update the pattern work item title to include the newly created ID
            if (newWorkItemId > 0 && patternItemId > 0)
            {
                await UpdatePatternItemWithId(patternItemId, title, isReleaseTrain, newWorkItemId);
            }
        }
        
        Console.WriteLine(new string('=', 80));
        
        // Short delay to ensure logs are readable and any API rate limits are respected
        await Task.Delay(100);
    }

    /// <summary>
    /// Updates an existing epic/release train by adding missing child relations
    /// </summary>
    private async Task UpdateExistingWorkItemWithChildren(int existingWorkItemId, List<int> children, string title, bool isReleaseTrain)
    {
        var workItemType = isReleaseTrain ? "Release Train" : "Epic";
        Console.WriteLine($"UPDATING EXISTING {workItemType.ToUpper()}: #{existingWorkItemId} - \"{title}\"");
        Console.WriteLine($"WITH {children.Count} CHILDREN: {string.Join(", ", children)}");
        
        _logger.LogInformation("Updating existing {Type} #{Id} with {Count} children", workItemType, existingWorkItemId, children.Count);
        
        // Track the update operation
        OperationsSummary.Operations.Add(new EpicReleaseTrainOperation
        {
            Type = workItemType,
            Operation = OperationType.Updated,
            Title = title,
            Id = existingWorkItemId,
            TotalWorkItems = children.Count,
            NewRelationsAdded = children.Count // For now, assume all are new (could be enhanced to check existing relations)
        });
        
        // TODO: Implement logic to check existing relations and only add missing ones
        // For now, we'll create relations to all children (some might already exist)
        foreach (var childId in children)
        {
            try
            {
                await _azureDevOpsService.CreateRelationAsync(existingWorkItemId, childId, "Child relation from pattern processing");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to create relation from {ParentId} to {ChildId} (may already exist)", existingWorkItemId, childId);
            }
        }
    }

    /// <summary>
    /// Creates a new epic/release train from the pattern
    /// </summary>
    private async Task<int> CreateNewWorkItemFromPattern(List<int> children, string title, bool isReleaseTrain, int patternItemId)
    {
        var workItemType = isReleaseTrain ? "Release Train" : "Epic";
        Console.WriteLine($"CREATING NEW {workItemType.ToUpper()}: \"{title}\"");
        Console.WriteLine($"WITH {children.Count} CHILDREN: {string.Join(", ", children)}");
        
        _logger.LogInformation("Creating new {Type}: {Title} with {Count} children", workItemType, title, children.Count);
        
        int newWorkItemId;
        if (isReleaseTrain)
        {
            newWorkItemId = await _azureDevOpsService.CreateReleaseTrainAsync(new List<int>(children), title, patternItemId);
        }
        else
        {
            newWorkItemId = await _azureDevOpsService.CreateEpicAsync(new List<int>(children), title, patternItemId);
        }
        
        // Track the creation operation
        if (newWorkItemId > 0)
        {
            OperationsSummary.Operations.Add(new EpicReleaseTrainOperation
            {
                Type = workItemType,
                Operation = OperationType.Created,
                Title = title,
                Id = newWorkItemId,
                TotalWorkItems = children.Count,
                NewRelationsAdded = children.Count
            });
        }
        
        return newWorkItemId;
    }

    /// <summary>
    /// Updates the pattern work item title to include the newly created work item ID
    /// </summary>
    private async Task UpdatePatternItemWithId(int patternItemId, string title, bool isReleaseTrain, int newWorkItemId)
    {
        var suffix = isReleaseTrain ? "rt" : "e";
        var newTitle = $"----- {title} -----{suffix}:{newWorkItemId}";
        
        _logger.LogInformation("Updating pattern item #{PatternItemId} title to include created work item ID #{NewWorkItemId}", 
            patternItemId, newWorkItemId);
        
        await _azureDevOpsService.UpdateWorkItemTitleAsync(patternItemId, newTitle);
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
        return roadmapItems
            .OrderBy(item => item.StackRank ?? double.MaxValue) // Sort by StackRank (ASC) first
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
