using ADOBuddyTool.Application.ErrorHandling;
using ADOBuddyTool.Domain.Entities;
using ADOBuddyTool.Presentation.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADOBuddyTool.Application.Commands;

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

            if (!options.Quiet)
            {
                _logger.LogInformation("Starting ADO hygiene checks on {Count} work items", workItems.Count());
                Console.WriteLine("\n" + "=".PadRight(separatorWidth, '='));
                Console.WriteLine("RUNNING ADO HYGIENE CHECKS");
                Console.WriteLine("=".PadRight(separatorWidth, '='));
            }

            var hygieneResults = await _services.Hygiene.PerformHygieneChecksAsync(workItems);
            DisplayHygieneCheckResults(hygieneResults, separatorWidth); return CommandResult.SuccessResult("Hygiene checks completed successfully", hygieneResults);
        }
        catch (Exception ex)
        {
            var error = _services.ErrorHandler.HandleException(ex, new Dictionary<string, object>
            {
                ["Operation"] = "Hygiene Checks",
                ["WorkItemCount"] = workItems.Count(),
                ["Options"] = options
            });

            _logger.LogError(ex, "Error during hygiene checks: {ErrorCode}", error.Code);

            // Display user-friendly error message
            Console.WriteLine($"\n❌ {error.UserFriendlyMessage}");
            if (error.RecoveryActions.Any())
            {
                Console.WriteLine($"💡 {string.Join("\n💡 ", error.RecoveryActions)}");
            }

            return CommandResult.FailureResult(error.UserFriendlyMessage);
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
            Console.WriteLine($"Warning Issues: {hygieneResults.WarningIssues} 🟡");        // Display breakdown by recommendation for failed checks
        var failedChecksByRecommendation = hygieneResults.CheckResults
            .Where(r => !r.Passed)
            .GroupBy(r => r.Recommendation)
            .Where(g => g.Any())
            .ToList();

        if (failedChecksByRecommendation.Any())
        {
            Console.WriteLine();
            Console.WriteLine("ISSUES BY CHECK TYPE");
            Console.WriteLine("-".PadRight(separatorWidth, '-'));

            foreach (var recommendationGroup in failedChecksByRecommendation.OrderByDescending(g => g.Count()))
            {
                var severityIcon = GetMostSevereIcon(recommendationGroup);
                Console.WriteLine($"{severityIcon} {recommendationGroup.Key}: {recommendationGroup.Count()} issues");

                // List work items under each recommendation
                foreach (var check in recommendationGroup.OrderBy(c => c.WorkItemId))
                {
                    Console.WriteLine($"{check.WorkItemUrl} - {check.WorkItemTitle}");
                }
                Console.WriteLine();
            }
        }
    }
    private static string GetMostSevereIcon(IGrouping<string, HygieneCheckResult> recommendationGroup)
    {
        var mostSevere = recommendationGroup.Max(c => c.Severity);
        return mostSevere switch
        {
            HygieneCheckSeverity.Critical => "🔴",
            HygieneCheckSeverity.Error => "🟠",
            HygieneCheckSeverity.Warning => "🟡",
            _ => "ℹ️"
        };
    }
}

