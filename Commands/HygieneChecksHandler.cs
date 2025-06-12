using CreateRoadmapADO.Models;
using CreateRoadmapADO.Services;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Commands;

/// <summary>
/// Handles hygiene check operations
/// </summary>
public class HygieneChecksHandler : ICommandHandler
{
    private readonly ServiceContainer _services;
    private readonly ILogger<HygieneChecksHandler> _logger;

    public string CommandName => "Hygiene Checks";

    public HygieneChecksHandler(ServiceContainer services, ILogger<HygieneChecksHandler> logger)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public bool ShouldExecute(CommandLineOptions options) => options.RunHygieneChecks;

    public async Task<CommandResult> ExecuteAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options)
    {
        try
        {
            const int separatorWidth = 60;

            if (!options.Summary)
            {
                _logger.LogInformation("Starting ADO hygiene checks on {Count} work items", workItems.Count());
                Console.WriteLine("\n" + "=".PadRight(separatorWidth, '='));
                Console.WriteLine("RUNNING ADO HYGIENE CHECKS");
                Console.WriteLine("=".PadRight(separatorWidth, '='));
            }

            var hygieneResults = await _services.Hygiene.PerformHygieneChecksAsync(workItems);
            DisplayHygieneCheckResults(hygieneResults, separatorWidth);

            return CommandResult.SuccessResult("Hygiene checks completed successfully", hygieneResults);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during hygiene checks");
            return CommandResult.FailureResult($"Hygiene checks failed: {ex.Message}");
        }
    }

    private void DisplayHygieneCheckResults(HygieneCheckSummary hygieneResults, int separatorWidth)
    {
        // Display summary
        Console.WriteLine();
        Console.WriteLine("HYGIENE CHECK SUMMARY");
        Console.WriteLine("=".PadRight(separatorWidth, '='));
        Console.WriteLine($"Total Checks: {hygieneResults.TotalChecks}");
        Console.WriteLine($"Passed: {hygieneResults.PassedChecks} ✅");
        Console.WriteLine($"Failed: {hygieneResults.FailedChecks} ❌");
        Console.WriteLine($"Health Score: {hygieneResults.HealthScore:F1}%");

        if (hygieneResults.CriticalIssues > 0)
            Console.WriteLine($"Critical Issues: {hygieneResults.CriticalIssues} 🔴");
        if (hygieneResults.ErrorIssues > 0)
            Console.WriteLine($"Error Issues: {hygieneResults.ErrorIssues} 🟠");
        if (hygieneResults.WarningIssues > 0)
            Console.WriteLine($"Warning Issues: {hygieneResults.WarningIssues} 🟡");

        // Display breakdown by check type for failed checks
        var failedChecksByType = hygieneResults.CheckResults
            .Where(r => !r.Passed)
            .GroupBy(r => r.CheckName)
            .Where(g => g.Any())
            .ToList();

        if (failedChecksByType.Any())
        {
            Console.WriteLine();
            Console.WriteLine("ISSUES BY CHECK TYPE");
            Console.WriteLine("-".PadRight(separatorWidth, '-'));

            foreach (var checkGroup in failedChecksByType.OrderByDescending(g => g.Count()))
            {
                var severityIcon = GetMostSevereIcon(checkGroup);
                Console.WriteLine($"{severityIcon} {checkGroup.Key}: {checkGroup.Count()} issues");
            }
        }

        Console.WriteLine();

        // Display failed checks in detail
        var failedChecks = hygieneResults.CheckResults.Where(r => !r.Passed).ToList();
        if (failedChecks.Any())
        {
            Console.WriteLine("FAILED CHECKS");
            Console.WriteLine("-".PadRight(separatorWidth, '-'));

            foreach (var check in failedChecks.OrderByDescending(c => c.Severity))
            {
                var severityIcon = check.Severity switch
                {
                    HygieneCheckSeverity.Critical => "🔴",
                    HygieneCheckSeverity.Error => "🟠",
                    HygieneCheckSeverity.Warning => "🟡",
                    _ => "ℹ️"
                };
                Console.WriteLine($"{severityIcon} [{check.Severity.ToString().ToUpper()}] {check.CheckName}");
                Console.WriteLine($"   Work Item: #{check.WorkItemId} - {check.WorkItemTitle}");
                Console.WriteLine($"   URL: {check.WorkItemUrl}");
                Console.WriteLine($"   Issue: {check.Details}");
                Console.WriteLine($"   Recommendation: {check.Recommendation}");
                Console.WriteLine();
            }
        }
    }

    private static string GetMostSevereIcon(IGrouping<string, HygieneCheckResult> checkGroup)
    {
        var mostSevere = checkGroup.Max(c => c.Severity);
        return mostSevere switch
        {
            HygieneCheckSeverity.Critical => "🔴",
            HygieneCheckSeverity.Error => "🟠",
            HygieneCheckSeverity.Warning => "🟡",
            _ => "ℹ️"
        };
    }
}
