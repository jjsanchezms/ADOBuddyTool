using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Service for performing comprehensive Azure DevOps hygiene checks on Release Train work items.
/// 
/// This service validates data quality, consistency, and completeness across Release Trains
/// and their associated Feature work items. It helps identify common project management issues
/// such as misaligned iteration paths, missing documentation, incomplete Release Train setup,
/// and inconsistent work item states.
/// 
/// The service supports multiple relationship types between Release Trains and Features:
/// - System.LinkTypes.Related (Related links)
/// - System.LinkTypes.Hierarchy-Forward (Parent-Child where Release Train is parent)
/// - System.LinkTypes.Hierarchy-Reverse (Child-Parent where Feature links to Release Train)
/// 
/// All checks produce detailed results with severity levels (Info, Warning, Error, Critical)
/// and actionable recommendations for remediation.
/// </summary>
public class HygieneCheckService
{    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ILogger<HygieneCheckService> _logger;
    private const string AzureDevOpsBaseUrl = "https://skype.visualstudio.com/SPOOL/_workitems/edit";

    /// <summary>
    /// Initializes a new instance of the HygieneCheckService with required dependencies.
    /// </summary>
    /// <param name="azureDevOpsService">Service for Azure DevOps API operations (retrieving work items and relationships)</param>
    /// <param name="logger">Logger for diagnostic information, debugging, and audit trails</param>
    /// <exception cref="ArgumentNullException">Thrown when any required dependency is null</exception>
    public HygieneCheckService(IAzureDevOpsService azureDevOpsService, ILogger<HygieneCheckService> logger)
    {
        _azureDevOpsService = azureDevOpsService ?? throw new ArgumentNullException(nameof(azureDevOpsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }    /// <summary>
    /// Generates the Azure DevOps URL for a work item
    /// </summary>
    /// <param name="workItemId">Work item ID</param>
    /// <returns>Full URL to the work item in Azure DevOps</returns>
    private static string GenerateWorkItemUrl(int workItemId)
    {
        return $"{AzureDevOpsBaseUrl}/{workItemId}";
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
    }/// <summary>
    /// Performs comprehensive hygiene checks on Release Trains and their related/child features.
    /// This method is the main entry point for all ADO hygiene validation checks.
    /// 
    /// The hygiene checks include:
    /// - Iteration path alignment between Release Trains and Features
    /// - Status notes/description currency and adequacy
    /// - Release Train completeness (feature count, tagging)
    /// - Feature state consistency with Release Train state
    /// 
    /// Only Release Train work items are evaluated as the primary subjects of hygiene checks.
    /// The method retrieves full relationship information for each Release Train to analyze
    /// both Related and Parent-Child relationships with Feature work items.
    /// </summary>
    /// <param name="workItems">Collection of work items (Features and Release Trains) to analyze</param>
    /// <param name="cancellationToken">Cancellation token for async operation</param>
    /// <returns>HygieneCheckSummary containing all check results, pass/fail counts, and detailed findings</returns>
    public async Task<HygieneCheckSummary> PerformHygieneChecksAsync(IEnumerable<WorkItem> workItems, CancellationToken cancellationToken = default)
    {
        var summary = new HygieneCheckSummary();
        var workItemsList = workItems.ToList();

        _logger.LogInformation("Starting hygiene checks on {Count} work items", workItemsList.Count);        // Find all Release Trains, excluding separator/placeholder patterns
        var releaseTrains = workItemsList
            .Where(w => w.WorkItemType == "Release Train" && !IsSeparatorPattern(w.Title))
            .ToList();
        
        // Log any separator patterns found for debugging
        var separatorPatterns = workItemsList
            .Where(w => w.WorkItemType == "Release Train" && IsSeparatorPattern(w.Title))
            .ToList();
            
        if (separatorPatterns.Any())
        {
            _logger.LogInformation("Excluding {Count} Release Train separator patterns from hygiene checks: {Titles}", 
                separatorPatterns.Count, 
                string.Join(", ", separatorPatterns.Select(sp => $"#{sp.Id} ({sp.Title})")));
        }
        
        if (!releaseTrains.Any())
        {
            _logger.LogInformation("No Release Trains found for hygiene checks (after excluding separator patterns)");
            return summary;
        }

        _logger.LogInformation("Found {Count} Release Trains to check (excluding {ExcludedCount} separator patterns)", 
            releaseTrains.Count, separatorPatterns.Count);

        foreach (var releaseTrain in releaseTrains)
        {
            try
            {
                // Get the Release Train with its relations
                var releaseTrainWithRelations = await _azureDevOpsService.GetWorkItemWithRelationsAsync(releaseTrain.Id, cancellationToken);
                  if (releaseTrainWithRelations?.Relations == null)
                {                    summary.CheckResults.Add(new HygieneCheckResult
                    {
                        CheckName = "Release Train Relations",
                        Passed = false,
                        Severity = HygieneCheckSeverity.Warning,
                        Description = "Check if Release Train has related or child work items",
                        Details = "No relations found for this Release Train",
                        WorkItemId = releaseTrain.Id,
                        WorkItemTitle = releaseTrain.Title,
                        WorkItemUrl = GenerateWorkItemUrl(releaseTrain.Id),
                        Recommendation = "Ensure this Release Train has related or child Feature work items using Related, Parent-Child, or Child-Parent relationships"
                    });
                    continue;
                }                // Get related and child feature IDs (using both Related and Child relationship types for hygiene checks)
                var allRelations = releaseTrainWithRelations.Relations.ToList();
                
                // Log all relationship types found for debugging
                var relationshipTypes = allRelations.Select(r => r.Rel).Distinct().ToList();
                _logger.LogInformation("Release Train {Id} has relationships of types: {RelationshipTypes}", 
                    releaseTrain.Id, string.Join(", ", relationshipTypes));
                
                var relatedFeatureIds = releaseTrainWithRelations.Relations
                    .Where(r => r.Rel == "System.LinkTypes.Related" || 
                               r.Rel == "System.LinkTypes.Hierarchy-Forward" ||
                               r.Rel == "System.LinkTypes.Hierarchy-Reverse")
                    .Select(r => r.GetRelatedWorkItemId())
                    .Where(id => id > 0)
                    .ToList();

                _logger.LogInformation("Found {Count} related/child work items for Release Train {Id} using relationship types: Related, Hierarchy-Forward, Hierarchy-Reverse", 
                    relatedFeatureIds.Count, releaseTrain.Id);

                // Get the actual feature work items
                var relatedFeatures = new List<WorkItem>();
                foreach (var featureId in relatedFeatureIds)
                {
                    var feature = await _azureDevOpsService.GetWorkItemByIdAsync(featureId, cancellationToken);
                    if (feature != null && feature.WorkItemType == "Feature")
                    {
                        relatedFeatures.Add(feature);
                    }
                }                // Perform specific hygiene checks
                await CheckReleaseTrainCompleteness(summary, releaseTrainWithRelations, relatedFeatures);
                await CheckStatusNotesUpToDate(summary, releaseTrainWithRelations, relatedFeatures);
                await CheckFeatureStateConsistency(summary, releaseTrainWithRelations, relatedFeatures);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing hygiene checks for Release Train {Id}", releaseTrain.Id);                summary.CheckResults.Add(new HygieneCheckResult
                {
                    CheckName = "Hygiene Check Error",
                    Passed = false,
                    Severity = HygieneCheckSeverity.Error,
                    Description = "Error occurred during hygiene check",
                    Details = $"Exception: {ex.Message}",
                    WorkItemId = releaseTrain.Id,
                    WorkItemTitle = releaseTrain.Title,
                    WorkItemUrl = GenerateWorkItemUrl(releaseTrain.Id),
                    Recommendation = "Review work item permissions and data integrity"
                });
            }
        }

        _logger.LogInformation("Completed hygiene checks. {PassedChecks}/{TotalChecks} checks passed", 
            summary.PassedChecks, summary.TotalChecks);

        return summary;
    }    /// <summary>
    /// Validates Release Train structural completeness and proper configuration.
    /// 
    /// This comprehensive check ensures Release Trains are properly set up and contain adequate content:
    /// 
    /// 1. Basic Validation:
    ///    - Verifies Release Train has at least one related/child Feature
    ///    - Empty Release Trains are flagged as warnings (likely incomplete setup)
    /// 
    /// 2. Iteration Path Alignment:
    ///    - Validates that Release Train iteration paths align with their related/child Feature iteration paths
    ///    - Ensures project planning consistency by verifying compatible time periods
    ///    - Allows exact matches, hierarchical relationships, and case-insensitive comparison
    /// 
    /// These validations help maintain data quality and ensure Release Trains
    /// serve their intended purpose as containers for related work.
    /// </summary>
    /// <param name="summary">HygieneCheckSummary to add results to</param>
    /// <param name="releaseTrain">Release Train work item to validate</param>
    /// <param name="relatedFeatures">Collection of related/child Feature work items</param>
    /// <returns>Completed task</returns>
    private Task CheckReleaseTrainCompleteness(HygieneCheckSummary summary, WorkItem releaseTrain, List<WorkItem> relatedFeatures)
    {        // Check if Release Train has related features (foundational check)
        CheckRelatedFeaturesExist(summary, releaseTrain, relatedFeatures);
        
        // If we have features, perform additional checks
        if (relatedFeatures.Any())
        {
            CheckIterationPathAlignment(summary, releaseTrain, relatedFeatures);
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Validates that the Release Train has at least one related/child Feature.
    /// Empty Release Trains are flagged as warnings since they likely represent incomplete setup.
    /// </summary>
    /// <param name="summary">HygieneCheckSummary to add results to</param>
    /// <param name="releaseTrain">Release Train work item to validate</param>
    /// <param name="relatedFeatures">Collection of related/child Feature work items</param>
    private void CheckRelatedFeaturesExist(HygieneCheckSummary summary, WorkItem releaseTrain, List<WorkItem> relatedFeatures)
    {
        var checkName = "Release Train Feature Count";
        var featureCount = relatedFeatures.Count;
        var hasAdequateFeatures = featureCount >= 1;        summary.CheckResults.Add(new HygieneCheckResult
        {
            CheckName = checkName,
            Passed = hasAdequateFeatures,
            Severity = hasAdequateFeatures ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = "Check if Release Train has adequate number of related features",
            Details = $"Release Train has {featureCount} related features",
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = GenerateWorkItemUrl(releaseTrain.Id),
            Recommendation = hasAdequateFeatures
                ? "Feature count looks appropriate"
                : "Release Train should have at least one related feature"
        });
    }

    /// <summary>
    /// Validates that Release Train iteration paths align with their related/child Feature iteration paths.
    /// 
    /// This check ensures project planning consistency by verifying that Release Trains and their
    /// associated Features are planned for compatible time periods. The validation allows for:
    /// - Exact iteration path matches
    /// - Hierarchical path relationships (parent/child iterations)
    /// - Case-insensitive comparison
    /// 
    /// Severity levels:
    /// - Error: Release Train has no iteration path assigned
    /// - Warning: Iteration paths don't align
    /// - Info: Iteration paths are properly aligned
    /// </summary>
    /// <param name="summary">HygieneCheckSummary to add results to</param>
    /// <param name="releaseTrain">Release Train work item to check</param>
    /// <param name="relatedFeatures">Collection of related/child Feature work items</param>
    private void CheckIterationPathAlignment(HygieneCheckSummary summary, WorkItem releaseTrain, List<WorkItem> relatedFeatures)
    {
        var checkName = "Iteration Path Alignment";
        
        var releaseTrainIterationPath = releaseTrain.IterationPath?.Trim();
        var featureIterationPaths = relatedFeatures
            .Where(f => !string.IsNullOrWhiteSpace(f.IterationPath))
            .Select(f => f.IterationPath!.Trim())
            .Distinct()
            .ToList();

        if (string.IsNullOrWhiteSpace(releaseTrainIterationPath))
        {            summary.CheckResults.Add(new HygieneCheckResult
            {
                CheckName = checkName,
                Passed = false,
                Severity = HygieneCheckSeverity.Error,
                Description = "Check if Release Train has iteration path set",
                Details = "Release Train does not have an iteration path assigned",
                WorkItemId = releaseTrain.Id,
                WorkItemTitle = releaseTrain.Title,
                WorkItemUrl = GenerateWorkItemUrl(releaseTrain.Id),
                Recommendation = "Set an appropriate iteration path for this Release Train"
            });
            return;
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
            WorkItemUrl = GenerateWorkItemUrl(releaseTrain.Id),
            Recommendation = hasMatchingIteration 
                ? "Iteration path alignment is good"
                : "Consider aligning Release Train iteration path with related features or vice versa"
        });}

    /// <summary>
    /// Analyzes state consistency between Release Trains and their related/child Features.
    /// 
    /// This check identifies potential project management issues by detecting misaligned states:
    /// 
    /// State Analysis:
    /// - Categorizes Features into New, Active, and Completed buckets
    /// - Calculates completion rate across all related Features
    /// - Identifies inconsistencies with Release Train state
    /// 
    /// Consistency Rules:
    /// - Release Trains marked as "Done/Closed/Completed" should have ≥80% Features completed
    /// - Release Trains in "New/Proposed" state shouldn't have active or completed Features
    /// - Warns when Release Train state doesn't reflect actual Feature progress
    /// 
    /// This helps project managers identify:
    /// - Release Trains marked complete prematurely
    /// - Stale Release Train states that need updating
    /// - Potential planning or tracking issues
    /// </summary>
    /// <param name="summary">HygieneCheckSummary to add results to</param>
    /// <param name="releaseTrain">Release Train work item to analyze</param>
    /// <param name="relatedFeatures">Collection of related/child Feature work items for state comparison</param>
    /// <returns>Completed task</returns>
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
            WorkItemUrl = GenerateWorkItemUrl(releaseTrain.Id),
            Recommendation = isConsistent
                ? "State consistency looks good"
                : "Review Release Train state to ensure it reflects the actual progress of related features"
        });
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Evaluates the quality and currency of status documentation for Release Trains and their Features.
    /// 
    /// This check performs two main validations:
    /// 1. Release Train Description Quality: Ensures the Release Train has adequate status notes
    ///    - Checks for presence of description
    ///    - Validates minimum length (>20 characters) for meaningful content
    /// 
    /// 2. Feature Documentation Coverage: Analyzes documentation completeness across related Features
    ///    - Calculates percentage of Features with descriptions
    ///    - Sets target coverage at 80% for passing grade
    ///    - Identifies specific Features lacking documentation
    /// 
    /// This helps ensure project stakeholders have sufficient information for decision-making
    /// and status reporting.
    /// </summary>
    /// <param name="summary">HygieneCheckSummary to add results to</param>
    /// <param name="releaseTrain">Release Train work item to evaluate</param>
    /// <param name="relatedFeatures">Collection of related/child Feature work items to analyze</param>
    /// <returns>Completed task</returns>
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
            WorkItemUrl = GenerateWorkItemUrl(releaseTrain.Id),
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
                WorkItemUrl = GenerateWorkItemUrl(releaseTrain.Id),
                Recommendation = descriptionCoverage >= 80
                    ? "Feature documentation coverage is good"
                    : $"Consider adding descriptions to {featuresWithoutDescription.Count} features without documentation"
            });
        }
        
        return Task.CompletedTask;
    }
}
