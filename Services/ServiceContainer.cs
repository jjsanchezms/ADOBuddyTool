using CreateRoadmapADO.Domain.Services;
using CreateRoadmapADO.ErrorHandling;
using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Services.AzureDevOps;
using CreateRoadmapADO.Services.HygieneChecks;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Simple service container to reduce dependency injection complexity
/// </summary>
public class ServiceContainer
{
    // Core services
    public IErrorHandler ErrorHandler { get; }

    // Infrastructure services
    public IAzureDevOpsService AzureDevOps { get; }
    public RoadmapService Roadmap { get; }
    public OutputService Output { get; }
    public HygieneCheckService Hygiene { get; }

    // Focused Azure DevOps services (for direct access when needed)
    public IAzureDevOpsQueryService QueryService { get; }
    public IAzureDevOpsWorkItemService WorkItemService { get; }
    public IAzureDevOpsRelationService RelationService { get; }
    public ISwagService SwagService { get; }

    // Domain services
    public IWorkItemDomainService WorkItemDomain { get; }
    public IReleaseTrainDomainService ReleaseTrainDomain { get; }
    public ISwagDomainService SwagDomain { get; }
    public ServiceContainer(ILoggerFactory loggerFactory)
    {
        // Create core services first
        ErrorHandler = new ErrorHandler(loggerFactory.CreateLogger<ErrorHandler>());

        // Create domain services (they have no dependencies on infrastructure)
        WorkItemDomain = new WorkItemDomainService(loggerFactory.CreateLogger<WorkItemDomainService>());
        SwagDomain = new SwagDomainService(loggerFactory.CreateLogger<SwagDomainService>());
        ReleaseTrainDomain = new ReleaseTrainDomainService(
            loggerFactory.CreateLogger<ReleaseTrainDomainService>(),
            WorkItemDomain);        // Create focused Azure DevOps services
        QueryService = new AzureDevOpsQueryService(loggerFactory.CreateLogger<AzureDevOpsQueryService>());
        RelationService = new AzureDevOpsRelationService(loggerFactory.CreateLogger<AzureDevOpsRelationService>());
        SwagService = new SwagService(); WorkItemService = new AzureDevOpsWorkItemService(
            loggerFactory.CreateLogger<AzureDevOpsWorkItemService>(),
            RelationService,
            SwagService);

        // Create composite service for backward compatibility
        AzureDevOps = new CompositeAzureDevOpsService(
            QueryService,
            WorkItemService,
            RelationService,
            loggerFactory.CreateLogger<CompositeAzureDevOpsService>());

        Roadmap = new RoadmapService(loggerFactory.CreateLogger<RoadmapService>(), AzureDevOps);

        Output = new OutputService(loggerFactory.CreateLogger<OutputService>());

        // Create hygiene check services
        var iterationCheck = new IterationPathAlignmentCheck(loggerFactory.CreateLogger<IterationPathAlignmentCheck>());
        var releaseTrainCheck = new ReleaseTrainCompletenessCheck(iterationCheck, loggerFactory.CreateLogger<ReleaseTrainCompletenessCheck>());
        var statusNotesCheck = new StatusNotesDocumentationCheck(loggerFactory.CreateLogger<StatusNotesDocumentationCheck>());
        var featureStateCheck = new FeatureStateConsistencyCheck(loggerFactory.CreateLogger<FeatureStateConsistencyCheck>());

        Hygiene = new HygieneCheckService(AzureDevOps, loggerFactory.CreateLogger<HygieneCheckService>(),
            releaseTrainCheck, statusNotesCheck, featureStateCheck);
    }
}
