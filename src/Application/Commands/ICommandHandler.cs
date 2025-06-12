using CreateRoadmapADO.Domain.Entities;

namespace CreateRoadmapADO.Application.Commands;

/// <summary>
/// Interface for command handlers that process work items based on user options
/// </summary>
public interface ICommandHandler
{
    /// <summary>
    /// Gets the name of this command for logging and display purposes
    /// </summary>
    string CommandName { get; }

    /// <summary>
    /// Determines if this handler should process the given options
    /// </summary>
    /// <param name="options">Command line options</param>
    /// <returns>True if this handler should execute</returns>
    bool ShouldExecute(CommandLineOptions options);

    /// <summary>
    /// Executes the command with the provided work items
    /// </summary>
    /// <param name="workItems">Work items to process</param>
    /// <param name="options">Command line options</param>
    /// <returns>Command execution result</returns>
    Task<CommandResult> ExecuteAsync(IEnumerable<WorkItem> workItems, CommandLineOptions options);
}

/// <summary>
/// Result of command execution
/// </summary>
public class CommandResult
{
    public bool Success { get; set; } = true;
    public string? Message { get; set; }
    public object? Data { get; set; }

    public static CommandResult SuccessResult(string? message = null, object? data = null)
        => new() { Success = true, Message = message, Data = data };

    public static CommandResult FailureResult(string message)
        => new() { Success = false, Message = message };
}

