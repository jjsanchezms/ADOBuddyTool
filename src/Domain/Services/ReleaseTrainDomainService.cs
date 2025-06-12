using CreateRoadmapADO.Domain.Entities;
using CreateRoadmapADO.Domain.Services;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace CreateRoadmapADO.Domain.Services;

/// <summary>
/// Domain service for Release Train business operations
/// Encapsulates Release Train-specific business logic and rules
/// </summary>
public class ReleaseTrainDomainService : IReleaseTrainDomainService
{
    private readonly ILogger<ReleaseTrainDomainService> _logger;
    private readonly IWorkItemDomainService _workItemDomainService;

    public ReleaseTrainDomainService(
        ILogger<ReleaseTrainDomainService> logger,
        IWorkItemDomainService workItemDomainService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workItemDomainService = workItemDomainService ?? throw new ArgumentNullException(nameof(workItemDomainService));
    }

    public bool IsValidReleaseTrainTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return false;
        }

        // Release Train titles should follow a pattern like "Q2 2024 Release Train" or similar
        var pattern = @"^(Q[1-4]\s+\d{4}|[A-Za-z]+\s+\d{4})\s+Release\s+Train";
        return Regex.IsMatch(title, pattern, RegexOptions.IgnoreCase);
    }

    public string GenerateReleaseTrainTitle(WorkItem patternWorkItem, IEnumerable<WorkItem> features)
    {
        if (patternWorkItem == null)
        {
            throw new ArgumentNullException(nameof(patternWorkItem));
        }

        var featureList = features.ToList();

        // Extract release pattern from the pattern work item
        var baseTitle = ExtractReleasePattern(patternWorkItem.Title);

        if (string.IsNullOrEmpty(baseTitle))
        {
            // Fallback to using current quarter if no pattern found
            var currentQuarter = GetCurrentQuarter();
            baseTitle = $"{currentQuarter} Release Train";
        }
        else if (!baseTitle.Contains("Release Train", StringComparison.OrdinalIgnoreCase))
        {
            baseTitle += " Release Train";
        }

        _logger.LogInformation("Generated Release Train title: {Title} for {FeatureCount} features",
            baseTitle, featureList.Count);

        return baseTitle;
    }

    public ReleaseTrainGroupingResult ShouldGroupFeaturesIntoReleaseTrain(IEnumerable<WorkItem> features)
    {
        var featureList = features.ToList();

        if (!featureList.Any())
        {
            return new ReleaseTrainGroupingResult
            {
                ShouldGroup = false,
                Reason = "No features provided for grouping"
            };
        }

        if (featureList.Count < 2)
        {
            return new ReleaseTrainGroupingResult
            {
                ShouldGroup = false,
                Reason = "Single features don't require Release Train grouping",
                EstimatedFeatureCount = featureList.Count
            };
        }

        // Check if features have similar iteration paths or themes
        var hasCommonIteration = HasCommonIterationPath(featureList);
        var hasCommonTheme = HasCommonTheme(featureList);

        if (hasCommonIteration || hasCommonTheme)
        {
            return new ReleaseTrainGroupingResult
            {
                ShouldGroup = true,
                Reason = hasCommonIteration ? "Features share common iteration path" : "Features share common theme",
                EstimatedFeatureCount = featureList.Count
            };
        }

        return new ReleaseTrainGroupingResult
        {
            ShouldGroup = featureList.Count >= 3, // Group larger collections by default
            Reason = featureList.Count >= 3 ? "Multiple features benefit from Release Train coordination" : "Features lack common attributes",
            EstimatedFeatureCount = featureList.Count
        };
    }

    public ReleaseTrainValidationResult ValidateReleaseTrainCompleteness(WorkItem releaseTrain, IEnumerable<WorkItem> relatedFeatures)
    {
        var result = new ReleaseTrainValidationResult();
        var featureList = relatedFeatures.ToList();

        result.ActualFeatureCount = featureList.Count;
        result.ExpectedFeatureCount = EstimateExpectedFeatureCount(releaseTrain);

        // Check if Release Train has sufficient features
        if (result.ActualFeatureCount == 0)
        {
            result.Issues.Add("Release Train has no associated features");
            result.IsComplete = false;
        }
        else if (result.ActualFeatureCount < result.ExpectedFeatureCount)
        {
            result.Issues.Add($"Release Train may be incomplete: has {result.ActualFeatureCount} features, expected approximately {result.ExpectedFeatureCount}");
        }

        // Validate feature states
        var closedFeatures = featureList.Count(f => f.State == "Closed");
        var activeFeatures = featureList.Count(f => f.State != "Closed" && f.State != "Removed");

        if (releaseTrain.State == "Active" && closedFeatures > activeFeatures)
        {
            result.Issues.Add("Active Release Train has more closed features than active ones");
        }

        // Validate SWAG consistency
        var featuresWithSwag = featureList.Count(f => f.Swag.HasValue);
        if (featuresWithSwag > 0 && featuresWithSwag < featureList.Count)
        {
            result.Issues.Add($"Only {featuresWithSwag} of {featureList.Count} features have SWAG values");
        }

        result.IsComplete = !result.Issues.Any();
        return result;
    }

    public bool ShouldUpdateReleaseTrainSwag(WorkItem releaseTrain, bool allMode)
    {
        if (releaseTrain.WorkItemType != "Release Train")
        {
            return false;
        }

        // In ALL mode, update all Release Trains
        if (allMode)
        {
            _logger.LogDebug("SWAG update approved for Release Train {WorkItemId} (ALL mode)", releaseTrain.Id);
            return true;
        }

        // In normal mode, only update auto-generated Release Trains
        var isAutoGenerated = _workItemDomainService.IsAutoGenerated(releaseTrain);

        if (!isAutoGenerated)
        {
            _logger.LogDebug("SWAG update skipped for Release Train {WorkItemId} (not auto-generated)", releaseTrain.Id);
        }

        return isAutoGenerated;
    }

    private string ExtractReleasePattern(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return string.Empty;
        }

        // Look for quarter patterns like "Q1 2024", "Q2 2024", etc.
        var quarterMatch = Regex.Match(title, @"Q[1-4]\s+\d{4}", RegexOptions.IgnoreCase);
        if (quarterMatch.Success)
        {
            return quarterMatch.Value + " Release Train";
        }

        // Look for year patterns
        var yearMatch = Regex.Match(title, @"\d{4}", RegexOptions.IgnoreCase);
        if (yearMatch.Success)
        {
            var currentQuarter = GetCurrentQuarter().Split(' ')[0]; // Extract just the quarter part
            return $"{currentQuarter} {yearMatch.Value} Release Train";
        }

        return string.Empty;
    }

    private string GetCurrentQuarter()
    {
        var month = DateTime.Now.Month;
        var year = DateTime.Now.Year;
        var quarter = (month - 1) / 3 + 1;
        return $"Q{quarter} {year}";
    }

    private bool HasCommonIterationPath(List<WorkItem> features)
    {
        var iterationPaths = features
            .Where(f => !string.IsNullOrWhiteSpace(f.IterationPath))
            .Select(f => f.IterationPath)
            .Distinct()
            .ToList();

        // If more than half the features share an iteration path, consider it common
        return iterationPaths.Count == 1 ||
               iterationPaths.Any(path => features.Count(f => f.IterationPath == path) > features.Count / 2);
    }

    private bool HasCommonTheme(List<WorkItem> features)
    {
        // Simple heuristic: look for common keywords in titles
        var commonWords = ExtractCommonKeywords(features.Select(f => f.Title));
        return commonWords.Any();
    }

    private List<string> ExtractCommonKeywords(IEnumerable<string> titles)
    {
        var allWords = titles
            .SelectMany(title => title.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(word => word.Length > 3) // Only consider meaningful words
            .GroupBy(word => word.ToLowerInvariant())
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToList();

        return allWords;
    }

    private int EstimateExpectedFeatureCount(WorkItem releaseTrain)
    {
        // Simple heuristic based on Release Train naming and status
        if (_workItemDomainService.IsAutoGenerated(releaseTrain))
        {
            return 2; // Auto-generated ones are typically for small groups
        }

        // For manually created Release Trains, expect more features
        return 3;
    }
}
