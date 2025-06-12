using CreateRoadmapADO.Infrastructure.AzureDevOps.Interfaces;
using CreateRoadmapADO.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Infrastructure.AzureDevOps.Services;

/// <summary>
/// Composite service that implements IAzureDevOpsService by delegating to focused services
/// This maintains backward compatibility while using the new service architecture internally
/// </summary>
public class CompositeAzureDevOpsService : IAzureDevOpsService
{
    private readonly IAzureDevOpsQueryService _queryService;
    private readonly IAzureDevOpsWorkItemService _workItemService;
    private readonly IAzureDevOpsRelationService _relationService;
    private readonly ILogger<CompositeAzureDevOpsService> _logger;

    public CompositeAzureDevOpsService(
        IAzureDevOpsQueryService queryService,
        IAzureDevOpsWorkItemService workItemService,
        IAzureDevOpsRelationService relationService,
        ILogger<CompositeAzureDevOpsService> logger)
    {
        _queryService = queryService ?? throw new ArgumentNullException(nameof(queryService));
        _workItemService = workItemService ?? throw new ArgumentNullException(nameof(workItemService));
        _relationService = relationService ?? throw new ArgumentNullException(nameof(relationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    #region Query Operations

    public Task<IEnumerable<WorkItem>> GetWorkItemsAsync(CancellationToken cancellationToken = default)
        => _queryService.GetWorkItemsAsync(cancellationToken);

    public Task<IEnumerable<WorkItem>> GetWorkItemsAsync(int limit, string areaPath, CancellationToken cancellationToken = default)
        => _queryService.GetWorkItemsAsync(limit, areaPath, cancellationToken);

    public Task<IEnumerable<WorkItem>> GetWorkItemsAsync(int limit, string areaPath, string workItemType, CancellationToken cancellationToken = default)
        => _queryService.GetWorkItemsAsync(limit, areaPath, workItemType, cancellationToken);

    public Task<IEnumerable<WorkItem>> GetWorkItemsForHygieneChecksAsync(int limit, string areaPath, CancellationToken cancellationToken = default)
        => _queryService.GetWorkItemsForHygieneChecksAsync(limit, areaPath, cancellationToken);

    public Task<WorkItem?> GetWorkItemByIdAsync(int workItemId, CancellationToken cancellationToken = default)
        => _queryService.GetWorkItemByIdAsync(workItemId, cancellationToken);

    public Task<WorkItem?> GetWorkItemWithRelationsAsync(int workItemId, CancellationToken cancellationToken = default)
        => _queryService.GetWorkItemWithRelationsAsync(workItemId, cancellationToken);

    public Task<int> GetExistingRelatedParentItemIdAsync(int workItemId, CancellationToken cancellationToken = default)
        => _relationService.GetExistingRelatedParentItemIdAsync(workItemId, cancellationToken); public Task<IEnumerable<WorkItem>> GetWorkItemsByQueryAsync(string wiqlQuery, CancellationToken cancellationToken = default)
        => _queryService.GetWorkItemsByQueryAsync(wiqlQuery, cancellationToken);

    public Task<IEnumerable<WorkItem>> GetWorkItemsForSwagUpdatesAsync(int limit, string areaPath, CancellationToken cancellationToken = default)
        => _queryService.GetWorkItemsForSwagUpdatesAsync(limit, areaPath, cancellationToken);

    #endregion

    #region Work Item Operations

    public Task<int> CreateReleaseTrainAsync(List<int> children, string title, string areaPath, int patternItemId = 0)
        => _workItemService.CreateReleaseTrainAsync(children, title, areaPath, patternItemId);

    public Task UpdateWorkItemTitleAsync(int workItemId, string newTitle)
        => _workItemService.UpdateWorkItemTitleAsync(workItemId, newTitle);

    public Task UpdateWorkItemSwagAsync(int workItemId, double swagValue)
        => _workItemService.UpdateWorkItemSwagAsync(workItemId, swagValue);

    public Task UpdateWorkItemStatusNotesWithSwagAsync(int workItemId, double swagValue, string originalStatusNotes)
        => _workItemService.UpdateWorkItemStatusNotesWithSwagAsync(workItemId, swagValue, originalStatusNotes);

    #endregion

    #region Relation Operations

    public Task CreateRelationAsync(int sourceId, int targetId, string comment = "")
        => _relationService.CreateRelationAsync(sourceId, targetId, comment);

    #endregion

    #region IDisposable

    public void Dispose()
    {
        _queryService?.Dispose();
        _workItemService?.Dispose();
        _relationService?.Dispose();
        GC.SuppressFinalize(this);
    }

    #endregion
}

