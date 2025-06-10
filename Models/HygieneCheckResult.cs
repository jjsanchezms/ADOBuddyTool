namespace CreateRoadmapADO.Models;

/// <summary>
/// Represents the result of a hygiene check
/// </summary>
public class HygieneCheckResult
{
    /// <summary>
    /// The name of the hygiene check
    /// </summary>
    public string CheckName { get; set; } = string.Empty;

    /// <summary>
    /// Whether the check passed
    /// </summary>
    public bool Passed { get; set; }

    /// <summary>
    /// Severity level of the check failure
    /// </summary>
    public HygieneCheckSeverity Severity { get; set; }

    /// <summary>
    /// Description of what was checked
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Details about the check result
    /// </summary>
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Work item ID this check applies to
    /// </summary>
    public int WorkItemId { get; set; }    /// <summary>
    /// Work item title for context
    /// </summary>
    public string WorkItemTitle { get; set; } = string.Empty;

    /// <summary>
    /// Azure DevOps URL to the work item
    /// </summary>
    public string WorkItemUrl { get; set; } = string.Empty;

    /// <summary>
    /// Recommendations for fixing the issue
    /// </summary>
    public string Recommendation { get; set; } = string.Empty;
}

/// <summary>
/// Severity levels for hygiene check failures
/// </summary>
public enum HygieneCheckSeverity
{
    Info,
    Warning,
    Error,
    Critical
}

/// <summary>
/// Summary of all hygiene checks performed
/// </summary>
public class HygieneCheckSummary
{
    /// <summary>
    /// All hygiene check results
    /// </summary>
    public List<HygieneCheckResult> CheckResults { get; set; } = new();

    /// <summary>
    /// Total number of checks performed
    /// </summary>
    public int TotalChecks => CheckResults.Count;

    /// <summary>
    /// Number of checks that passed
    /// </summary>
    public int PassedChecks => CheckResults.Count(r => r.Passed);

    /// <summary>
    /// Number of checks that failed
    /// </summary>
    public int FailedChecks => CheckResults.Count(r => !r.Passed);

    /// <summary>
    /// Number of critical issues found
    /// </summary>
    public int CriticalIssues => CheckResults.Count(r => !r.Passed && r.Severity == HygieneCheckSeverity.Critical);

    /// <summary>
    /// Number of error issues found
    /// </summary>
    public int ErrorIssues => CheckResults.Count(r => !r.Passed && r.Severity == HygieneCheckSeverity.Error);

    /// <summary>
    /// Number of warning issues found
    /// </summary>
    public int WarningIssues => CheckResults.Count(r => !r.Passed && r.Severity == HygieneCheckSeverity.Warning);

    /// <summary>
    /// Overall health score (percentage of passed checks)
    /// </summary>
    public double HealthScore => TotalChecks > 0 ? (double)PassedChecks / TotalChecks * 100 : 0;

    /// <summary>
    /// Gets a breakdown of failed checks grouped by check name and severity
    /// </summary>
    /// <returns>Dictionary with severity levels and their issue breakdowns</returns>
    public Dictionary<HygieneCheckSeverity, Dictionary<string, List<HygieneCheckResult>>> GetIssueBreakdown()
    {
        var breakdown = new Dictionary<HygieneCheckSeverity, Dictionary<string, List<HygieneCheckResult>>>();

        var failedChecks = CheckResults.Where(r => !r.Passed).ToList();

        foreach (var severity in Enum.GetValues<HygieneCheckSeverity>())
        {
            var severityChecks = failedChecks.Where(r => r.Severity == severity).ToList();
            if (severityChecks.Any())
            {
                breakdown[severity] = severityChecks
                    .GroupBy(r => r.CheckName)
                    .ToDictionary(g => g.Key, g => g.ToList());
            }
        }

        return breakdown;
    }

    /// <summary>
    /// Gets a summary of issues by type for a specific severity level
    /// </summary>
    /// <param name="severity">The severity level to filter by</param>
    /// <returns>Dictionary with check names and their counts</returns>
    public Dictionary<string, int> GetIssueSummaryBySeverity(HygieneCheckSeverity severity)
    {
        return CheckResults
            .Where(r => !r.Passed && r.Severity == severity)
            .GroupBy(r => r.CheckName)
            .ToDictionary(g => g.Key, g => g.Count());
    }

    /// <summary>
    /// Gets issues grouped by a common pattern in the details (for summary categorization)
    /// </summary>
    /// <param name="severity">The severity level to filter by</param>
    /// <returns>Dictionary with issue patterns and their counts</returns>
    public Dictionary<string, int> GetIssuePatternSummary(HygieneCheckSeverity severity)
    {
        var issues = CheckResults.Where(r => !r.Passed && r.Severity == severity).ToList();
        var patterns = new Dictionary<string, int>();

        foreach (var issue in issues)
        {
            var pattern = GetIssuePattern(issue);
            patterns[pattern] = patterns.GetValueOrDefault(pattern, 0) + 1;
        }

        return patterns.OrderByDescending(kvp => kvp.Value).ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <summary>
    /// Extracts a pattern description from a hygiene check result for categorization
    /// </summary>
    private static string GetIssuePattern(HygieneCheckResult result)
    {
        // Only categorize failed checks - passed checks shouldn't be in pattern summaries
        if (result.Passed)
            return result.CheckName; // This shouldn't normally be called for passed checks

        var checkName = result.CheckName;
        var details = result.Details?.ToLowerInvariant() ?? "";

        return checkName switch
        {
            "Status Notes Currency" when details.Contains("no description") => "No description provided",
            "Status Notes Currency" when details.Contains("description present") => "Description too short",
            "Release Train Completeness" when details.Contains("0 related features") => "No related features",
            "Release Train Completeness" when details.Contains("related features") => "Insufficient features",
            "Iteration Path Alignment" when details.Contains("does not have an iteration path") => "No iteration path set",
            "Iteration Path Alignment" when details.Contains("does not match") => "Iteration path mismatch",
            "Feature Documentation Coverage" => "Poor feature documentation",
            "Feature State Consistency" when details.Contains("state") => "State inconsistency",
            "Release Train Relations" when details.Contains("no relations") => "No work item relations",
            "Hygiene Check Error" => "Check execution error",
            _ => checkName // Default to the check name
        };
    }
}
