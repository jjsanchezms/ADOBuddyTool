using CreateRoadmapADO.Domain.Services;
using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Services.HygieneChecks;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Simple service container to reduce dependency injection complexity
/// </summary>
public class ServiceContainer
{
    // Infrastructure services
    public IAzureDevOpsService AzureDevOps { get; }
    public RoadmapService Roadmap { get; }
    public OutputService Output { get; }
    public HygieneCheckService Hygiene { get; }

    // Domain services
    public IWorkItemDomainService WorkItemDomain { get; }
    public IReleaseTrainDomainService ReleaseTrainDomain { get; }
    public ISwagDomainService SwagDomain { get; }

    public ServiceContainer(ILoggerFactory loggerFactory)
    {
        // Create domain services first (they have no dependencies on infrastructure)
        WorkItemDomain = new WorkItemDomainService(loggerFactory.CreateLogger<WorkItemDomainService>());
        SwagDomain = new SwagDomainService(loggerFactory.CreateLogger<SwagDomainService>());
        ReleaseTrainDomain = new ReleaseTrainDomainService(
            loggerFactory.CreateLogger<ReleaseTrainDomainService>(),
            WorkItemDomain);

        // Create infrastructure services
        AzureDevOps = new AzureDevOpsService(loggerFactory.CreateLogger<AzureDevOpsService>());

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
