using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using CreateRoadmapADO.Configuration;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Service for performing Azure DevOps hygiene checks on work items
/// </summary>
public class HygieneCheckService
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ILogger<HygieneCheckService> _logger;
    private readonly AzureDevOpsOptions _options;

    public HygieneCheckService(IAzureDevOpsService azureDevOpsService, ILogger<HygieneCheckService> logger)
    {
        _azureDevOpsService = azureDevOpsService ?? throw new ArgumentNullException(nameof(azureDevOpsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = ConfigurationReader.GetAzureDevOpsOptions();    }    /// <summary>
    /// Generates the Azure DevOps URL for a work item
    /// </summary>
    /// <param name="workItemId">The work item ID</param>
    /// <returns>The full Azure DevOps URL to the work item</returns>
    private string GetWorkItemUrl(int workItemId)
    {
        return $"https://{_options.Organization}.visualstudio.com/{_options.Project}/_workitems/edit/{workItemId}";
    }

    /// <summary>
    /// Determines if a Release Train title follows a separator/placeholder pattern that should be ignored for hygiene checks.
    /// These are typically formatting elements like "----------------------------- CY25 -----------------------------"
    /// </summary>
    /// <param name="title">Release Train title to check</param>
    /// <returns>True if the title appears to be a separator/placeholder pattern</returns>
    private static bool IsSeparatorPattern(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return false;

        var cleanTitle = title.Trim();
        
        // Check if title contains mostly dashes and/or percent signs with minimal text content
        // Pattern examples: "-------- CY25 --------", "%%%%% Q1 %%%%%", "--- FY25 ---"
        var dashCount = cleanTitle.Count(c => c == '-');
        var percentCount = cleanTitle.Count(c => c == '%');
        var separatorCount = dashCount + percentCount;
        
        // If more than 60% of the title is separators (dashes/percents), consider it a separator pattern
        var separatorRatio = (double)separatorCount / cleanTitle.Length;
        
        return separatorRatio > 0.6;
    }

    /// <summary>
    /// Performs comprehensive hygiene checks on Release Trains and their related features
    /// </summary>
    /// <param name="workItems">All work items to check</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Summary of hygiene check results</returns>
    public async Task<HygieneCheckSummary> PerformHygieneChecksAsync(IEnumerable<WorkItem> workItems, CancellationToken cancellationToken = default)
    {
        var summary = new HygieneCheckSummary();
        var workItemsList = workItems.ToList();

        _logger.LogInformation("Starting hygiene checks on {Count} work items", workItemsList.Count);        // Find all Release Trains
        var allReleaseTrains = workItemsList.Where(w => w.WorkItemType == "Release Train").ToList();
        
        if (!allReleaseTrains.Any())
        {
            _logger.LogInformation("No Release Trains found for hygiene checks");
            return summary;
        }

        // Filter out separator pattern Release Trains
        var releaseTrains = allReleaseTrains.Where(rt => !IsSeparatorPattern(rt.Title)).ToList();
        var separatorPatterns = allReleaseTrains.Where(rt => IsSeparatorPattern(rt.Title)).ToList();

        if (separatorPatterns.Any())
        {
            _logger.LogInformation("Excluding {Count} separator pattern Release Trains from hygiene checks: {Titles}", 
                separatorPatterns.Count,
                string.Join(", ", separatorPatterns.Select(sp => $"'{sp.Title}'")));
        }

        if (!releaseTrains.Any())
        {
            _logger.LogInformation("No non-separator Release Trains found for hygiene checks");
            return summary;
        }

        _logger.LogInformation("Found {Count} Release Trains to check (excluded {ExcludedCount} separator patterns)", 
            releaseTrains.Count, separatorPatterns.Count);

        foreach (var releaseTrain in releaseTrains)
        {
            try
            {
                // Get the Release Train with its relations
                var releaseTrainWithRelations = await _azureDevOpsService.GetWorkItemWithRelationsAsync(releaseTrain.Id, cancellationToken);
                
                if (releaseTrainWithRelations?.Relations == null)                {                    summary.CheckResults.Add(new HygieneCheckResult
                    {
                        CheckName = "Release Train Relations",
                        Passed = false,
                        Severity = HygieneCheckSeverity.Warning,
                        Description = "Check if Release Train has related or child work items",
                        Details = "No relations found for this Release Train",
                        WorkItemId = releaseTrain.Id,
                        WorkItemTitle = releaseTrain.Title,
                        WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
                        Recommendation = "Ensure this Release Train has related or child Feature work items"
                    });
                    continue;
                }                // Get related and child feature IDs
                var allRelations = releaseTrainWithRelations.Relations.ToList();
                _logger.LogDebug("Release Train #{Id} has {Count} total relations: {Relations}", 
                    releaseTrain.Id, allRelations.Count, 
                    string.Join(", ", allRelations.Select(r => $"{r.Rel}→{r.GetRelatedWorkItemId()}")));

                var relatedFeatureIds = releaseTrainWithRelations.Relations
                    .Where(r => r.Rel == "System.LinkTypes.Related" || r.Rel == "System.LinkTypes.Hierarchy-Forward")
                    .Select(r => r.GetRelatedWorkItemId())
                    .Where(id => id > 0)
                    .ToList();

                _logger.LogDebug("Release Train #{Id} found {Count} related/child IDs: {Ids}", 
                    releaseTrain.Id, relatedFeatureIds.Count, string.Join(", ", relatedFeatureIds));                // Get the actual feature work items
                var relatedFeatures = new List<WorkItem>();
                foreach (var featureId in relatedFeatureIds)
                {
                    var feature = await _azureDevOpsService.GetWorkItemByIdAsync(featureId, cancellationToken);
                    if (feature != null && feature.WorkItemType == "Feature")
                    {
                        relatedFeatures.Add(feature);
                        _logger.LogDebug("Added Feature #{Id}: {Title}", feature.Id, feature.Title);
                    }
                    else if (feature != null)
                    {
                        _logger.LogDebug("Skipped work item #{Id} (type: {Type}): {Title}", 
                            feature.Id, feature.WorkItemType, feature.Title);
                    }
                    else
                    {
                        _logger.LogWarning("Could not retrieve work item #{Id}", featureId);
                    }
                }

                _logger.LogInformation("Release Train #{Id} has {Count} related Features", 
                    releaseTrain.Id, relatedFeatures.Count);

                // Perform specific hygiene checks
                await CheckIterationPathAlignment(summary, releaseTrainWithRelations, relatedFeatures);
                await CheckStatusNotesUpToDate(summary, releaseTrainWithRelations, relatedFeatures);
                await CheckReleaseTrainCompleteness(summary, releaseTrainWithRelations, relatedFeatures);
                await CheckFeatureStateConsistency(summary, releaseTrainWithRelations, relatedFeatures);
            }
            catch (Exception ex)
            {                _logger.LogError(ex, "Error performing hygiene checks for Release Train {Id}", releaseTrain.Id);
                summary.CheckResults.Add(new HygieneCheckResult
                {
                    CheckName = "Hygiene Check Error",
                    Passed = false,
                    Severity = HygieneCheckSeverity.Error,
                    Description = "Error occurred during hygiene check",
                    Details = $"Exception: {ex.Message}",
                    WorkItemId = releaseTrain.Id,
                    WorkItemTitle = releaseTrain.Title,
                    WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
                    Recommendation = "Review work item permissions and data integrity"
                });
            }
        }

        _logger.LogInformation("Completed hygiene checks. {PassedChecks}/{TotalChecks} checks passed", 
            summary.PassedChecks, summary.TotalChecks);

        return summary;
    }    /// <summary>
    /// Checks if Release Train iteration path matches at least one related feature's iteration path
    /// </summary>
    private Task CheckIterationPathAlignment(HygieneCheckSummary summary, WorkItem releaseTrain, List<WorkItem> relatedFeatures)
    {
        var checkName = "Iteration Path Alignment";
          if (!relatedFeatures.Any())
        {
            summary.CheckResults.Add(new HygieneCheckResult
            {
                CheckName = checkName,
                Passed = false,
                Severity = HygieneCheckSeverity.Warning,
                Description = "Check if Release Train has related features for iteration path validation",
                Details = "No related features found to compare iteration paths",
                WorkItemId = releaseTrain.Id,
                WorkItemTitle = releaseTrain.Title,
                WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
                Recommendation = "Add related Feature work items to this Release Train"
            });
            return Task.CompletedTask;
        }

        var releaseTrainIterationPath = releaseTrain.IterationPath?.Trim();
        var featureIterationPaths = relatedFeatures
            .Where(f => !string.IsNullOrWhiteSpace(f.IterationPath))
            .Select(f => f.IterationPath!.Trim())
            .Distinct()
            .ToList();        if (string.IsNullOrWhiteSpace(releaseTrainIterationPath))
        {
            summary.CheckResults.Add(new HygieneCheckResult
            {
                CheckName = checkName,
                Passed = false,
                Severity = HygieneCheckSeverity.Error,
                Description = "Check if Release Train has iteration path set",
                Details = "Release Train does not have an iteration path assigned",
                WorkItemId = releaseTrain.Id,
                WorkItemTitle = releaseTrain.Title,
                WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
                Recommendation = "Set an appropriate iteration path for this Release Train"
            });
            return Task.CompletedTask;
        }

        var hasMatchingIteration = featureIterationPaths.Any(fp => 
            string.Equals(fp, releaseTrainIterationPath, StringComparison.OrdinalIgnoreCase) ||
            fp.StartsWith(releaseTrainIterationPath, StringComparison.OrdinalIgnoreCase) ||
            releaseTrainIterationPath.StartsWith(fp, StringComparison.OrdinalIgnoreCase));        summary.CheckResults.Add(new HygieneCheckResult
        {
            CheckName = checkName,
            Passed = hasMatchingIteration,
            Severity = hasMatchingIteration ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = "Check if Release Train iteration path aligns with related features",
            Details = hasMatchingIteration 
                ? $"Release Train iteration '{releaseTrainIterationPath}' aligns with feature iterations"
                : $"Release Train iteration '{releaseTrainIterationPath}' does not match any feature iterations: {string.Join(", ", featureIterationPaths)}",
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
            Recommendation = hasMatchingIteration 
                ? "Iteration path alignment is good"
                : "Consider aligning Release Train iteration path with related features or vice versa"
        });
        
        return Task.CompletedTask;
    }    /// <summary>
    /// Checks if status notes/descriptions are up to date
    /// </summary>
    private Task CheckStatusNotesUpToDate(HygieneCheckSummary summary, WorkItem releaseTrain, List<WorkItem> relatedFeatures)
    {
        var checkName = "Status Notes Currency";

        // Check Release Train description
        var hasDescription = !string.IsNullOrWhiteSpace(releaseTrain.Description);
        var descriptionLength = releaseTrain.Description?.Trim().Length ?? 0;        summary.CheckResults.Add(new HygieneCheckResult
        {
            CheckName = checkName,
            Passed = hasDescription && descriptionLength > 20,
            Severity = hasDescription && descriptionLength > 20 ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = "Check if Release Train has adequate status notes/description",
            Details = hasDescription 
                ? $"Description present ({descriptionLength} characters)"
                : "No description provided",
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
            Recommendation = hasDescription && descriptionLength > 20
                ? "Status documentation looks adequate"
                : "Consider adding detailed status notes or description to provide context and current status"
        });

        // Check if features have descriptions
        var featuresWithoutDescription = relatedFeatures.Where(f => string.IsNullOrWhiteSpace(f.Description)).ToList();
        
        if (relatedFeatures.Any())
        {
            var descriptionCoverage = (double)(relatedFeatures.Count - featuresWithoutDescription.Count) / relatedFeatures.Count * 100;
              summary.CheckResults.Add(new HygieneCheckResult
            {
                CheckName = "Feature Documentation Coverage",
                Passed = descriptionCoverage >= 80,
                Severity = descriptionCoverage >= 80 ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
                Description = "Check if related features have adequate documentation",
                Details = $"{descriptionCoverage:F1}% of related features have descriptions ({relatedFeatures.Count - featuresWithoutDescription.Count}/{relatedFeatures.Count})",
                WorkItemId = releaseTrain.Id,
                WorkItemTitle = releaseTrain.Title,
                WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
                Recommendation = descriptionCoverage >= 80
                    ? "Feature documentation coverage is good"
                    : $"Consider adding descriptions to {featuresWithoutDescription.Count} features without documentation"
            });
        }
        
        return Task.CompletedTask;
    }    /// <summary>
    /// Checks Release Train completeness and quality
    /// </summary>
    private Task CheckReleaseTrainCompleteness(HygieneCheckSummary summary, WorkItem releaseTrain, List<WorkItem> relatedFeatures)
    {
        var checkName = "Release Train Completeness";

        // Check if Release Train has adequate number of features
        var featureCount = relatedFeatures.Count;        var hasAdequateFeatures = featureCount >= 1;        summary.CheckResults.Add(new HygieneCheckResult
        {
            CheckName = checkName,
            Passed = hasAdequateFeatures,
            Severity = hasAdequateFeatures ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Error,
            Description = "Check if Release Train has adequate number of related features",
            Details = $"Release Train has {featureCount} related features",
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
            Recommendation = hasAdequateFeatures
                ? "Feature count looks appropriate"
                : "Release Train should have at least one related feature"
        });
        
        return Task.CompletedTask;
    }    /// <summary>
    /// Checks consistency between Release Train and Feature states
    /// </summary>
    private Task CheckFeatureStateConsistency(HygieneCheckSummary summary, WorkItem releaseTrain, List<WorkItem> relatedFeatures)
    {
        var checkName = "Feature State Consistency";

        if (!relatedFeatures.Any())
        {
            return Task.CompletedTask; // Skip if no features
        }

        var releaseTrainState = releaseTrain.State?.ToLowerInvariant();
        var featureStates = relatedFeatures.Select(f => f.State?.ToLowerInvariant()).Where(s => !string.IsNullOrEmpty(s)).ToList();

        // Check for inconsistent states
        var completedFeatures = featureStates.Count(s => s == "done" || s == "closed" || s == "completed");
        var activeFeatures = featureStates.Count(s => s == "active" || s == "committed" || s == "in progress");
        var newFeatures = featureStates.Count(s => s == "new" || s == "proposed" || s == "approved");

        var totalFeatures = featureStates.Count;
        var completionRate = totalFeatures > 0 ? (double)completedFeatures / totalFeatures * 100 : 0;

        // Determine if Release Train state is consistent with feature states
        var isConsistent = true;
        var inconsistencyDetails = new List<string>();

        if (releaseTrainState == "done" || releaseTrainState == "closed" || releaseTrainState == "completed")
        {
            if (completionRate < 80)
            {
                isConsistent = false;
                inconsistencyDetails.Add($"Release Train marked as complete but only {completionRate:F1}% of features are done");
            }
        }
        else if (releaseTrainState == "new" || releaseTrainState == "proposed")
        {
            if (activeFeatures > 0 || completedFeatures > 0)
            {
                isConsistent = false;
                inconsistencyDetails.Add($"Release Train is in '{releaseTrain.State}' state but has {activeFeatures + completedFeatures} features that are active or complete");
            }
        }        summary.CheckResults.Add(new HygieneCheckResult
        {
            CheckName = checkName,
            Passed = isConsistent,
            Severity = isConsistent ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = "Check consistency between Release Train and feature states",
            Details = isConsistent 
                ? $"State consistency is good. RT: '{releaseTrain.State}', Features: {completedFeatures} done, {activeFeatures} active, {newFeatures} new"
                : string.Join("; ", inconsistencyDetails),
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = GetWorkItemUrl(releaseTrain.Id),
            Recommendation = isConsistent
                ? "State consistency looks good"
                : "Review Release Train state to ensure it reflects the actual progress of related features"
        });
        
        return Task.CompletedTask;
    }
}
