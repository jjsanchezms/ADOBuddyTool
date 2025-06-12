using ADOBuddyTool.Application.ErrorHandling;
using ADOBuddyTool.Domain.Entities;
using ADOBuddyTool.Presentation.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ADOBuddyTool.Application.Commands;

/// <summary>
/// Coordinates the execution of different command handlers based on user options
/// </summary>
public class CommandCoordinator
{
    private readonly ServiceContainer _services;
    private readonly ILogger<CommandCoordinator> _logger;
    private readonly List<ICommandHandler> _handlers;

    public CommandCoordinator(ServiceContainer services, ILoggerFactory loggerFactory)
    {
        _services = services ?? throw new ArgumentNullException(nameof(services));
        _logger = loggerFactory.CreateLogger<CommandCoordinator>();

        // Initialize all available command handlers
        _handlers = new List<ICommandHandler>
        {
            new RoadmapGenerationHandler(services, loggerFactory.CreateLogger<RoadmapGenerationHandler>()),
            new HygieneChecksHandler(services, loggerFactory.CreateLogger<HygieneChecksHandler>()),
            new SwagUpdatesHandler(services, loggerFactory.CreateLogger<SwagUpdatesHandler>())
        };
    }

    /// <summary>
    /// Executes all applicable commands based on the provided options
    /// </summary>
    /// <param name="workItems">Work items to process</param>
    /// <param name="options">Command line options</param>
    /// <returns>Collection of command results</returns>
    public async Task<List<CommandResult>> ExecuteCommandsAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options)
    {
        var results = new List<CommandResult>();
        var applicableHandlers = _handlers.Where(h => h.ShouldExecute(options)).ToList();

        if (!applicableHandlers.Any())
        {
            _logger.LogWarning("No applicable command handlers found for the given options");
            results.Add(CommandResult.FailureResult("No operations specified"));
            return results;
        }

        _logger.LogInformation("Executing {Count} commands: {Commands}",
            applicableHandlers.Count,
            string.Join(", ", applicableHandlers.Select(h => h.CommandName)));

        foreach (var handler in applicableHandlers)
        {
            try
            {
                _logger.LogInformation("Starting {CommandName}", handler.CommandName);
                var result = await handler.ExecuteAsync(workItems, options);
                results.Add(result);

                if (!result.Success)
                {
                    _logger.LogError("Command {CommandName} failed: {Message}", handler.CommandName, result.Message);
                }
                else
                {
                    _logger.LogInformation("Command {CommandName} completed successfully", handler.CommandName);
                }
            }
            catch (Exception ex)
            {
                var error = _services.ErrorHandler.HandleException(ex, new Dictionary<string, object>
                {
                    ["Operation"] = "Command Coordination",
                    ["CommandName"] = handler.CommandName,
                    ["WorkItemCount"] = workItems.Count()
                });

                _logger.LogError(ex, "Unexpected error executing command {CommandName}: {ErrorCode}",
                    handler.CommandName, error.Code);

                // Display user-friendly error message
                Console.WriteLine($"\n❌ {error.UserFriendlyMessage}");
                if (error.RecoveryActions.Any())
                {
                    Console.WriteLine($"💡 {string.Join("\n💡 ", error.RecoveryActions)}");
                }

                results.Add(CommandResult.FailureResult(error.UserFriendlyMessage));
            }
        }

        return results;
    }

    /// <summary>
    /// Gets roadmap items from the results if available
    /// </summary>
    /// <param name="results">Command execution results</param>
    /// <returns>Roadmap items if found, empty collection otherwise</returns>
    public IEnumerable<RoadmapItem> GetRoadmapItems(List<CommandResult> results)
    {
        var roadmapResult = results.FirstOrDefault(r => r.Success && r.Data is IEnumerable<RoadmapItem>);
        return roadmapResult?.Data as IEnumerable<RoadmapItem> ?? Enumerable.Empty<RoadmapItem>();
    }
}

