using ADOBuddyTool.Infrastructure.AzureDevOps.Interfaces;
using ADOBuddyTool.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace ADOBuddyTool.Infrastructure.HygieneChecks.Checks;

/// <summary>
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
public class ReleaseTrainCompletenessCheck : IHygieneCheck
{
    private readonly IterationPathAlignmentCheck _iterationPathCheck;
    private readonly ILogger<ReleaseTrainCompletenessCheck> _logger;

    public string CheckName => "Release Train Completeness";
    public string CheckDescription => "Validates Release Train structural completeness and proper configuration";

    public ReleaseTrainCompletenessCheck(
        IterationPathAlignmentCheck iterationPathCheck,
        ILogger<ReleaseTrainCompletenessCheck> logger)
    {
        _iterationPathCheck = iterationPathCheck ?? throw new ArgumentNullException(nameof(iterationPathCheck));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
    public async Task<IEnumerable<HygieneCheckResult>> PerformCheckAsync(HygieneCheckContext context, CancellationToken cancellationToken = default)
    {
        var releaseTrain = context.WorkItem;
        var relatedFeatures = context.RelatedFeatures;

        _logger.LogDebug("Checking completeness for Release Train {Id}: {Title}", releaseTrain.Id, releaseTrain.Title);

        var results = new List<HygieneCheckResult>();

        // Check if Release Train has related features (foundational check)
        _logger.LogDebug("Checking feature count for Release Train {Id}: {Title}", releaseTrain.Id, releaseTrain.Title);

        var featureCount = relatedFeatures.Count;
        var hasAdequateFeatures = featureCount >= 1;

        var featureCountResult = new HygieneCheckResult
        {
            CheckName = "Release Train Feature Count",
            Passed = hasAdequateFeatures,
            Severity = hasAdequateFeatures ? HygieneCheckSeverity.Info : HygieneCheckSeverity.Warning,
            Description = "Check if Release Train has adequate number of related features",
            Details = $"Release Train has {featureCount} related features",
            WorkItemId = releaseTrain.Id,
            WorkItemTitle = releaseTrain.Title,
            WorkItemUrl = HygieneCheckContext.GenerateWorkItemUrl(releaseTrain.Id),
            Recommendation = hasAdequateFeatures
                ? "Feature count looks appropriate"
                : "Release Train should have at least one related feature"
        };

        results.Add(featureCountResult);

        // If we have features, perform additional checks
        if (relatedFeatures.Any())
        {
            var iterationPathResults = await _iterationPathCheck.PerformCheckAsync(context, cancellationToken);
            results.AddRange(iterationPathResults);
        }

        return results;
    }
}

