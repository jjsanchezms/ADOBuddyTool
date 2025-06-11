using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;
using System.Linq;
using System.Text.RegularExpressions;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Service for generating and managing roadmaps from work items
/// </summary>
public class RoadmapService
{
    private readonly ILogger<RoadmapService> _logger;
    private readonly IAzureDevOpsService _azureDevOpsService;
    
    // Track release train operations for summary
    public ReleaseTrainSummary OperationsSummary { get; private set; } = new();

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
            OperationsSummary = new ReleaseTrainSummary
            {
                TotalBacklogItemsProcessed = workItems.Count(),
                BacklogReadSuccessfully = true
            };

            // Process special titles to create release trains
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
    }    /// <summary>
    /// Processes work items to identify special title patterns and create release trains
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
        int currentPatternItemId = 0;
        bool isCollectingItems = false;

        _logger.LogInformation("Scanning for special title patterns in work items");        foreach (var workItem in workItemsList)
        {            // Check for special title patterns
            // Match titles like "----- TITLE -----rt" with optional IDs like ":1234"
            // Only look for "rt" patterns (release trains)
            var patternStart = new Regex(@"^-+\s*(.*?)\s*-+rt(?::(\d+))?$");
            var match = patternStart.Match(workItem.Title);
              // Check if this is a section separator (like "CY25H1 Features Begin")
            var isSectionSeparator = workItem.Title.StartsWith("---") && workItem.Title.EndsWith("---") && 
                !workItem.Title.Contains("-----rt") && 
                (workItem.Title.Contains("Begin") || workItem.Title.Contains("End") || workItem.Title.Contains("Feature") || workItem.Title.Contains("CY25"));
                
            _logger.LogDebug("Processing work item {Id}: '{Title}' - IsReleaseTrain: {IsReleaseTrain}, IsSectionSeparator: {IsSectionSeparator}, IsCollecting: {IsCollecting}", 
                workItem.Id, workItem.Title, match.Success, isSectionSeparator, isCollectingItems);            if (match.Success)
            {
                // If we were already collecting items, create the previous release train
                if (isCollectingItems && currentTitle != null && currentChildren.Any())
                {
                    _logger.LogInformation("PATTERN DETECTED: Creating previous group '{CurrentTitle}' with {Count} children before starting new group '{NewTitle}'", 
                        currentTitle, currentChildren.Count, match.Groups[1].Value.Trim());
                    
                    // Log all children being added to the previous group
                    _logger.LogInformation("Children for '{CurrentTitle}': [{Children}]", 
                        currentTitle, string.Join(", ", currentChildren));
                    
                    await CreateItemFromPattern(currentChildren, currentTitle, currentExistingId, currentPatternItemId);
                    currentChildren.Clear();
                    
                    _logger.LogInformation("COMPLETED: Previous group '{PreviousTitle}' created. Children list cleared.", currentTitle);
                }

                // Extract new title from between the markers
                currentTitle = match.Groups[1].Value.Trim();
                
                // Store the pattern item ID for later renaming
                currentPatternItemId = workItem.Id;
                  // Extract existing work item ID if present
                currentExistingId = null;
                if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    if (int.TryParse(match.Groups[2].Value, out int parsedId))
                    {
                        currentExistingId = parsedId;
                        _logger.LogInformation("Pattern references existing Release Train ID: {ExistingId}", parsedId);
                    }
                    else
                    {
                        _logger.LogWarning("Pattern contains invalid ID format: '{InvalidId}' - will create new Release Train instead", match.Groups[2].Value);
                    }
                }
                else
                {
                    _logger.LogInformation("Pattern does not reference existing Release Train ID - will create new one");
                }
                
                isCollectingItems = true;

                _logger.LogInformation("STARTED: Now collecting for Release Train pattern: \"{Title}\"{IdInfo}", 
                    currentTitle, currentExistingId.HasValue ? $" (ID: {currentExistingId})" : "");
            }
            // Check if this is a section separator - these should end the current group
            else if (isSectionSeparator)
            {
                _logger.LogInformation("Found section separator: {Title}", workItem.Title);
                
                // If we were collecting items, create the release train
                if (isCollectingItems && currentTitle != null && currentChildren.Any())
                {
                    _logger.LogInformation("Ending group due to section separator: \"{Title}\" with {Count} children", 
                        currentTitle, currentChildren.Count);
                    await CreateItemFromPattern(currentChildren, currentTitle, currentExistingId, currentPatternItemId);
                    currentChildren.Clear();
                    isCollectingItems = false;
                }
            }
            // If we're collecting items and this isn't a special title, add it to current children
            else if (isCollectingItems)
            {
                // Check if this is an end marker without a new group (just dashes)
                // Match patterns that start and end with at least 3 dashes and don't contain "rt"
                var isEndMarker = workItem.Title.StartsWith("---") && workItem.Title.EndsWith("---") && 
                    !workItem.Title.Contains("-----rt");
                
                if (isEndMarker)
                {
                    _logger.LogInformation("Found end marker: {Title}", workItem.Title);
                    
                    // End current group
                    if (currentTitle != null && currentChildren.Any())
                    {
                        _logger.LogInformation("Ending group due to end marker: \"{Title}\" with {Count} children", 
                            currentTitle, currentChildren.Count);
                        await CreateItemFromPattern(currentChildren, currentTitle, currentExistingId, currentPatternItemId);
                        currentChildren.Clear();
                        isCollectingItems = false;
                    }
                }                else
                {
                    // Add to current children
                    currentChildren.Add(workItem.Id);
                    _logger.LogInformation("CHILD ADDED: Added work item {Id} to '{CurrentTitle}': '{Title}' (Total children: {Count})", 
                        workItem.Id, currentTitle ?? "UNKNOWN", workItem.Title, currentChildren.Count);
                }
            }
        }        // Don't forget to create the last release train if we were collecting items
        if (isCollectingItems && currentTitle != null && currentChildren.Any())
        {
            _logger.LogInformation("FINAL GROUP: Creating final group '{Title}' at end of processing with {Count} children", 
                currentTitle, currentChildren.Count);
            _logger.LogInformation("Final children for '{CurrentTitle}': [{Children}]", 
                currentTitle, string.Join(", ", currentChildren));
            await CreateItemFromPattern(currentChildren, currentTitle, currentExistingId, currentPatternItemId);
        }
    }    /// <summary>
    /// Creates a Release Train item from pattern items
    /// </summary>
    private async Task CreateItemFromPattern(List<int> children, string title, int? existingWorkItemId = null, int patternItemId = 0)
    {
        // Create a divider for better console readability
        Console.WriteLine(new string('=', 80));
        
        if (existingWorkItemId.HasValue)
        {
            // Update existing release train by adding missing children
            await UpdateExistingWorkItemWithChildren(existingWorkItemId.Value, children, title);
        }
        else
        {
            // Create new release train
            int newWorkItemId = await CreateNewWorkItemFromPattern(children, title, patternItemId);
            
            // Update the pattern work item title to include the newly created ID
            if (newWorkItemId > 0 && patternItemId > 0)
            {
                await UpdatePatternItemWithId(patternItemId, title, newWorkItemId);
            }
        }
        
        Console.WriteLine(new string('=', 80));
        
        // Short delay to ensure logs are readable and any API rate limits are respected
        await Task.Delay(100);
    }    /// <summary>
    /// Updates an existing release train by adding missing child relations
    /// </summary>
    private async Task UpdateExistingWorkItemWithChildren(int existingWorkItemId, List<int> children, string title)
    {
        Console.WriteLine($"UPDATING EXISTING RELEASE TRAIN: #{existingWorkItemId} - \"{title}\"");
        Console.WriteLine($"WITH {children.Count} CHILDREN: {string.Join(", ", children)}");
        
        _logger.LogInformation("Updating existing Release Train #{Id} with {Count} children from current pattern group", existingWorkItemId, children.Count);
        
        // First, validate that the release train actually exists
        _logger.LogInformation("Validating that Release Train #{Id} exists and is accessible", existingWorkItemId);
        
        // Get existing work item with relations to check what already exists
        var existingWorkItem = await _azureDevOpsService.GetWorkItemWithRelationsAsync(existingWorkItemId);
          if (existingWorkItem == null)
        {
            _logger.LogError("CRITICAL ERROR: Release Train #{Id} does not exist or is not accessible. Cannot create relations to non-existent work item.", existingWorkItemId);
            _logger.LogError("This indicates a data integrity issue where the pattern references a work item ID that doesn't exist.");
            _logger.LogWarning("AUTOMATIC RECOVERY: Creating a new Release Train instead of updating the non-existent one.");
            
            // Instead of failing, create a new release train
            Console.WriteLine($"❌ ERROR: Release Train #{existingWorkItemId} does not exist");
            Console.WriteLine($"🔄 RECOVERY: Creating new Release Train instead");
            
            try
            {
                int newWorkItemId = await CreateNewWorkItemFromPattern(children, title, 0);
                if (newWorkItemId > 0)
                {
                    _logger.LogInformation("✅ Successfully created new Release Train #{NewId} as replacement for non-existent #{OldId}", newWorkItemId, existingWorkItemId);
                    Console.WriteLine($"✅ Created new Release Train #{newWorkItemId} instead of #{existingWorkItemId}");
                }
                else
                {
                    _logger.LogError("❌ Failed to create replacement Release Train");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Failed to create replacement Release Train for non-existent #{Id}", existingWorkItemId);
            }
            
            return;
        }
        
        _logger.LogInformation("✅ Release Train #{Id} exists and is accessible. Proceeding with relation updates.", existingWorkItemId);
        
        var existingRelatedIds = new HashSet<int>();        
        if (existingWorkItem?.Relations != null)
        {
            foreach (var relation in existingWorkItem.Relations)
            {
                if (relation.Rel == "System.LinkTypes.Related")
                {
                    var relatedId = relation.GetRelatedWorkItemId();
                    if (relatedId > 0)
                    {
                        existingRelatedIds.Add(relatedId);
                    }
                }
            }
        }
        
        _logger.LogInformation("Existing Release Train #{Id} already has {ExistingCount} related items: [{ExistingItems}]", 
            existingWorkItemId, existingRelatedIds.Count, string.Join(", ", existingRelatedIds));
        
        // Check if we should replace existing relations instead of adding to them
        // If the current pattern only has a few specific children, it might be a targeted update
        var shouldReplaceRelations = children.Count <= 10; // Heuristic: small groups are usually targeted updates
        
        if (shouldReplaceRelations && existingRelatedIds.Any())
        {
            _logger.LogWarning("NOTICE: Existing Release Train #{Id} has {ExistingCount} relations, but current pattern only specifies {NewCount} children. Consider if this is intentional.", 
                existingWorkItemId, existingRelatedIds.Count, children.Count);
            
            // For now, we'll still just add new relations, but log the discrepancy
            // In the future, you might want to add logic to remove old relations that aren't in the current pattern
        }
        
        // Find children that don't already have relations
        var newChildren = children.Where(childId => !existingRelatedIds.Contains(childId)).ToList();
        
        _logger.LogInformation("From current pattern group, {NewCount} items are new: [{NewItems}]", 
            newChildren.Count, string.Join(", ", newChildren));
        
        // Track the update operation with accurate counts
        OperationsSummary.Operations.Add(new ReleaseTrainOperation
        {
            Type = "Release Train",
            Operation = OperationType.Updated,
            Title = title,
            Id = existingWorkItemId,
            TotalWorkItems = children.Count,
            NewRelationsAdded = newChildren.Count
        });
        
        // Only create relations for children that don't already have them
        if (newChildren.Any())
        {
            _logger.LogInformation("Adding {NewCount} new relations out of {TotalCount} children from current pattern", newChildren.Count, children.Count);
            
            foreach (var childId in newChildren)
            {
                try
                {
                    _logger.LogInformation("Creating relation from Release Train #{ParentId} to child #{ChildId}", existingWorkItemId, childId);
                    await _azureDevOpsService.CreateRelationAsync(existingWorkItemId, childId, "Child relation from pattern processing");
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to create relation from {ParentId} to {ChildId}", existingWorkItemId, childId);
                }
            }
        }
        else
        {
            _logger.LogInformation("All child relations from current pattern already exist for Release Train #{Id}", existingWorkItemId);
        }
    }/// <summary>
    /// Creates a new release train from the pattern
    /// </summary>
    private async Task<int> CreateNewWorkItemFromPattern(List<int> children, string title, int patternItemId)
    {
        Console.WriteLine($"CREATING NEW RELEASE TRAIN: \"{title}\"");
        Console.WriteLine($"WITH {children.Count} CHILDREN: {string.Join(", ", children)}");
        
        _logger.LogInformation("Creating new Release Train: {Title} with {Count} children", title, children.Count);
        
        int newWorkItemId = await _azureDevOpsService.CreateReleaseTrainAsync(new List<int>(children), title, patternItemId);
        
        // Track the creation operation
        if (newWorkItemId > 0)
        {
            OperationsSummary.Operations.Add(new ReleaseTrainOperation
            {
                Type = "Release Train",
                Operation = OperationType.Created,
                Title = title,
                Id = newWorkItemId,
                TotalWorkItems = children.Count,
                NewRelationsAdded = children.Count
            });
        }
          return newWorkItemId;    }
    
    /// <summary>
    /// Updates the pattern work item title to include the newly created work item ID
    /// </summary>
    private async Task UpdatePatternItemWithId(int patternItemId, string title, int newWorkItemId)
    {
        try
        {
            // Update the title to include the new work item ID
            var newTitle = $"----- {title} -----rt:{newWorkItemId}";
            await _azureDevOpsService.UpdateWorkItemTitleAsync(patternItemId, newTitle);
            
            _logger.LogInformation("Updated pattern item #{PatternItemId} title to include Release Train ID #{NewWorkItemId}", 
                patternItemId, newWorkItemId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update pattern item #{PatternItemId} with new Release Train ID #{NewWorkItemId}", 
                patternItemId, newWorkItemId);
        }
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
    }    public async Task CreateReleaseTrainAsync(List<int> children, string title)
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
            "release train" => RoadmapItemType.ReleaseTrain,
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
    {        // Simple estimation logic - could be enhanced with actual data
        var estimatedDays = workItemType.ToLowerInvariant() switch
        {
            "release train" => 90, // 3 months
            "feature" => 30, // 1 month
            "initiative" => 180, // 6 months
            _ => 14 // 2 weeks
        };

        return startDate.AddDays(estimatedDays);
    }}
