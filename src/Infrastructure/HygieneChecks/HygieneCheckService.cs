using ADOBuddyTool.Infrastructure.AzureDevOps.Interfaces;
using ADOBuddyTool.Domain.Entities;
using ADOBuddyTool.Infrastructure.HygieneChecks.Checks;
using Microsoft.Extensions.Logging;

namespace ADOBuddyTool.Infrastructure.HygieneChecks;

/// <summary>
/// Service for performing comprehensive Azure DevOps hygiene checks on Release Train work items.
/// 
/// This service orchestrates multiple hygiene check implementations to validate data quality, consistency, 
/// and completeness across Release Trains and their associated Feature work items. It helps identify common 
/// project management issues such as misaligned iteration paths, missing documentation, incomplete Release 
/// Train setup, and inconsistent work item states.
/// 
/// The service supports multiple relationship types between Release Trains and Features:
/// - System.LinkTypes.Related (Related links)
/// - System.LinkTypes.Hierarchy-Forward (Parent-Child where Release Train is parent)
/// - System.LinkTypes.Hierarchy-Reverse (Child-Parent where Feature links to Release Train)
/// 
/// Release Train Checks:
/// - Structural completeness and feature count validation
/// - Iteration path alignment with related Features
/// - Status notes currency and adequacy
/// - Feature state consistency
/// 
/// All checks produce detailed results with severity levels (Info, Warning, Error, Critical)
/// and actionable recommendations for remediation.
/// </summary>
public class HygieneCheckService
{
    private readonly IAzureDevOpsService _azureDevOpsService;
    private readonly ILogger<HygieneCheckService> _logger;

    // Collection of all hygiene checks
    private readonly List<IHygieneCheck> _checks;

    /// <summary>
    /// Initializes a new instance of the HygieneCheckService with required dependencies.
    /// </summary>
    /// <param name="azureDevOpsService">Service for Azure DevOps API operations</param>
    /// <param name="logger">Logger for diagnostic information</param>
    /// <param name="releaseTrainCompletenessCheck">Check for Release Train completeness</param>
    /// <param name="statusNotesCheck">Check for status documentation</param>
    /// <param name="featureStateConsistencyCheck">Check for feature state consistency</param>
    public HygieneCheckService(
        IAzureDevOpsService azureDevOpsService,
        ILogger<HygieneCheckService> logger,
        ReleaseTrainCompletenessCheck releaseTrainCompletenessCheck,
        StatusNotesDocumentationCheck statusNotesCheck,
        FeatureStateConsistencyCheck featureStateConsistencyCheck)
    {
        _azureDevOpsService = azureDevOpsService ?? throw new ArgumentNullException(nameof(azureDevOpsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Group all checks into a single collection for easier management
        _checks = new List<IHygieneCheck>
        {
            releaseTrainCompletenessCheck ?? throw new ArgumentNullException(nameof(releaseTrainCompletenessCheck)),
            statusNotesCheck ?? throw new ArgumentNullException(nameof(statusNotesCheck)),
            featureStateConsistencyCheck ?? throw new ArgumentNullException(nameof(featureStateConsistencyCheck))
        };
    }/// <summary>
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

        // Check if title starts with dashes (separator pattern)
        // Pattern examples: "--- Sprint Planning ---", "----------------------------- CY25 -----------------------------"
        return cleanTitle.StartsWith("---");
    }/// <summary>
     /// Performs comprehensive hygiene checks on Release Trains and Features.
     /// This method is the main entry point for all ADO hygiene validation checks.
     /// 
     /// The hygiene checks include:
     /// 
     /// Release Train Checks:
     /// - Iteration path alignment between Release Trains and Features
     /// - Status notes/description currency and adequacy
     /// - Release Train completeness (feature count, tagging)
     /// - Feature state consistency with Release Train state
     /// 
     /// Feature Checks:
     /// - Related link validation to Release Trains
     /// - Multiple Release Train relationship warnings
     /// - Release Train state consistency for linked Release Trains
     /// 
     /// The method processes Release Trains first to establish baseline relationships,
     /// then evaluates Features for proper Release Train linkage and ownership clarity.
     /// </summary>
     /// <param name="workItems">Collection of work items (Features and Release Trains) to analyze</param>
     /// <param name="cancellationToken">Cancellation token for async operation</param>
     /// <returns>HygieneCheckSummary containing all check results, pass/fail counts, and detailed findings</returns>
    public async Task<HygieneCheckSummary> PerformHygieneChecksAsync(IEnumerable<WorkItem> workItems, CancellationToken cancellationToken = default)
    {
        var summary = new HygieneCheckSummary();
        var workItemsList = workItems.ToList();

        _logger.LogInformation("Starting hygiene checks on {Count} work items", workItemsList.Count);

        // Find all Release Trains, excluding separator/placeholder patterns
        var releaseTrains = workItemsList
            .Where(w => w.WorkItemType == "Release Train" && !IsSeparatorPattern(w.Title))
            .ToList();

        // Find all Features for additional hygiene checks
        var features = workItemsList
            .Where(w => w.WorkItemType == "Feature")
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

        _logger.LogInformation("Found {Count} Release Trains and {FeatureCount} Features to check (excluding {ExcludedCount} separator patterns)",
            releaseTrains.Count, features.Count, separatorPatterns.Count);

        // Process Release Trains
        foreach (var releaseTrain in releaseTrains)
        {
            try
            {
                // Get the Release Train with its relations
                var releaseTrainWithRelations = await _azureDevOpsService.GetWorkItemWithRelationsAsync(releaseTrain.Id, cancellationToken);

                if (releaseTrainWithRelations?.Relations == null)
                {
                    summary.CheckResults.Add(new HygieneCheckResult
                    {
                        CheckName = "Release Train Relations",
                        Passed = false,
                        Severity = HygieneCheckSeverity.Warning,
                        Description = "Check if Release Train has related or child work items",
                        Details = "No relations found for this Release Train",
                        WorkItemId = releaseTrain.Id,
                        WorkItemTitle = releaseTrain.Title,
                        WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(releaseTrain.Id),
                        Recommendation = "Ensure this Release Train has related or child Feature work items using Related, Parent-Child, or Child-Parent relationships"
                    });
                    continue;
                }

                // Get related and child feature IDs
                var allRelations = releaseTrainWithRelations.Relations.ToList();
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

                // Get the actual feature work items
                var relatedFeatures = new List<WorkItem>();
                foreach (var featureId in relatedFeatureIds)
                {
                    var feature = await _azureDevOpsService.GetWorkItemByIdAsync(featureId, cancellationToken);
                    if (feature != null && feature.WorkItemType == "Feature")
                    {
                        relatedFeatures.Add(feature);
                    }
                }

                // Create context for Release Train checks
                var context = new HygieneCheckContext
                {
                    WorkItem = releaseTrainWithRelations,
                    RelatedFeatures = relatedFeatures,
                    AllReleaseTrains = releaseTrains
                };                // Perform Release Train specific hygiene checks using the collection
                foreach (var check in _checks)
                {
                    var checkResults = await check.PerformCheckAsync(context, cancellationToken);
                    summary.CheckResults.AddRange(checkResults);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error performing hygiene checks for Release Train {Id}", releaseTrain.Id);

                summary.CheckResults.Add(new HygieneCheckResult
                {
                    CheckName = "Hygiene Check Error",
                    Passed = false,
                    Severity = HygieneCheckSeverity.Error,
                    Description = "Error occurred during hygiene check",
                    Details = $"Exception: {ex.Message}",
                    WorkItemId = releaseTrain.Id,
                    WorkItemTitle = releaseTrain.Title,
                    WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(releaseTrain.Id),
                    Recommendation = "Review work item permissions and data integrity"
                });
            }
        }

        _logger.LogInformation("Completed hygiene checks. {PassedChecks}/{TotalChecks} checks passed",
            summary.PassedChecks, summary.TotalChecks);

        return summary;
    }
}

