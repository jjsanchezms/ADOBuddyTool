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
    /// Direct URL to the work item in Azure DevOps
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
}
