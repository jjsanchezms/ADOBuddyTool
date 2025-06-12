using Microsoft.Extensions.Logging;

namespace ADOBuddyTool.Services;

/// <summary>
/// Base service class with common logger setup
/// </summary>
public abstract class BaseService<T>
{
    protected readonly ILogger<T> Logger;

    protected BaseService(ILogger<T> logger)
    {
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
