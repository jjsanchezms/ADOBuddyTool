using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Models;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services.HygieneChecks;

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
    private readonly ReleaseTrainFeatureCountCheck _featureCountCheck;
    private readonly IterationPathAlignmentCheck _iterationPathCheck;
    private readonly ILogger<ReleaseTrainCompletenessCheck> _logger;

    public string CheckName => "Release Train Completeness";
    public string CheckDescription => "Validates Release Train structural completeness and proper configuration";

    public ReleaseTrainCompletenessCheck(
        ReleaseTrainFeatureCountCheck featureCountCheck,
        IterationPathAlignmentCheck iterationPathCheck,
        ILogger<ReleaseTrainCompletenessCheck> logger)
    {
        _featureCountCheck = featureCountCheck ?? throw new ArgumentNullException(nameof(featureCountCheck));
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
        var featureCountResults = await _featureCountCheck.PerformCheckAsync(context, cancellationToken);
        results.AddRange(featureCountResults);

        // If we have features, perform additional checks
        if (relatedFeatures.Any())
        {
            var iterationPathResults = await _iterationPathCheck.PerformCheckAsync(context, cancellationToken);
            results.AddRange(iterationPathResults);
        }

        return results;
    }
}
