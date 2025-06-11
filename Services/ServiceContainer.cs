using CreateRoadmapADO.Interfaces;
using CreateRoadmapADO.Services.HygieneChecks;
using Microsoft.Extensions.Logging;

namespace CreateRoadmapADO.Services;

/// <summary>
/// Simple service container to reduce dependency injection complexity
/// </summary>
public class ServiceContainer
{
    public IAzureDevOpsService AzureDevOps { get; }
    public RoadmapService Roadmap { get; }
    public OutputService Output { get; }
    public HygieneCheckService Hygiene { get; }
    public ServiceContainer(ILoggerFactory loggerFactory)
    {
        // Create all services with shared logger factory
        AzureDevOps = new AzureDevOpsService(loggerFactory.CreateLogger<AzureDevOpsService>());

        Roadmap = new RoadmapService(loggerFactory.CreateLogger<RoadmapService>(), AzureDevOps);

        Output = new OutputService(loggerFactory.CreateLogger<OutputService>());

        // Create hygiene check services using factory method
        Hygiene = CreateHygieneCheckService(loggerFactory);
    }    /// <summary>
         /// Creates and configures the hygiene check service with all required checks
         /// </summary>
         /// <param name="loggerFactory">Logger factory for creating individual loggers</param>
         /// <returns>Configured hygiene check service</returns>
    private HygieneCheckService CreateHygieneCheckService(ILoggerFactory loggerFactory)
    {
        // Create individual hygiene checks
        var checks = new List<IHygieneCheck>
        {
            new IterationPathAlignmentCheck(loggerFactory.CreateLogger<IterationPathAlignmentCheck>()),
            new StatusNotesDocumentationCheck(loggerFactory.CreateLogger<StatusNotesDocumentationCheck>()),
            new FeatureStateConsistencyCheck(loggerFactory.CreateLogger<FeatureStateConsistencyCheck>())
        };

        // Add release train completeness check with its dependency
        var iterationCheck = checks.OfType<IterationPathAlignmentCheck>().First();
        checks.Add(new ReleaseTrainCompletenessCheck(iterationCheck, loggerFactory.CreateLogger<ReleaseTrainCompletenessCheck>()));

        return new HygieneCheckService(AzureDevOps, loggerFactory.CreateLogger<HygieneCheckService>(), checks);
    }
}
