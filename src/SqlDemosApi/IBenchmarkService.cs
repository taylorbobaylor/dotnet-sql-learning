namespace SqlDemosApi;

public interface IBenchmarkService
{
    Task<ScenarioResult> RunScenarioAsync(int id, CancellationToken cancellationToken = default);
    Task<AllScenariosResult> RunAllAsync(CancellationToken cancellationToken = default);
}
