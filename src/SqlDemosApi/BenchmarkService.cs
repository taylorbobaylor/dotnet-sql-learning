using System.Diagnostics;

using Microsoft.Extensions.Logging;

using SqlDemos.Shared;

namespace SqlDemosApi;

/// <summary>
/// Executes bad-vs-fixed stored procedure pairs and returns structured timing results.
/// Scenario definitions are data-driven via <see cref="ScenarioDefinition"/> so adding a
/// new scenario requires only a new entry in <see cref="ScenarioCatalog"/> — no new methods.
/// </summary>
public sealed class BenchmarkService(
    IDbConnectionFactory connectionFactory,
    IProcTimer procTimer,
    TimeProvider timeProvider,
    ILogger<BenchmarkService> logger) : IBenchmarkService
{
    // Year - 1 is computed once at construction time (app startup) from the injected TimeProvider.
    // For a demo service this is precise enough; restart the service on New Year's Day.
    private readonly ScenarioDefinition[] _scenarios =
        ScenarioCatalog.Build(timeProvider.GetUtcNow().Year - 1);

    // ── Public API ───────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="Task"/> directly (no state machine) since there is no
    /// try/catch wrapping the dispatch and no post-await work in this method.
    /// </summary>
    public Task<ScenarioResult> RunScenarioAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id < 1 || id > ScenarioCatalog.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(id), $"Scenario id must be 1–{ScenarioCatalog.Count}, got {id}.");
        }

        return RunScenarioByDefinitionAsync(_scenarios[id - 1], cancellationToken);
    }

    /// <summary>
    /// Runs all scenarios concurrently with <see cref="Task.WhenAll"/>.
    /// Scenarios are independent (different stored procedures / plan caches) so
    /// parallel execution is safe. Results are re-ordered by Id before returning.
    /// </summary>
    public async Task<AllScenariosResult> RunAllAsync(CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        var tasks = _scenarios.Select(s => RunScenarioByDefinitionAsync(s, cancellationToken)).ToArray();
        ScenarioResult[] scenarioResults;

        try
        {
            scenarioResults = await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // Task.WhenAll re-throws only the first exception; log all failures for diagnostics.
            foreach (var faulted in tasks.Where(t => t.IsFaulted))
            {
                logger.LogError(faulted.Exception, "Scenario task faulted");
            }

            throw;
        }

        stopwatch.Stop();

        return new AllScenariosResult(
            stopwatch.ElapsedMilliseconds,
            scenarioResults.OrderBy(r => r.Id).ToList(),
            timeProvider.GetUtcNow());
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private async Task<ScenarioResult> RunScenarioByDefinitionAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken)
    {
        var runs = new List<ProcRun>(scenario.Runs.Count);

        foreach (var run in scenario.Runs)
        {
            try
            {
                var (ms, rowCount) = await procTimer.TimeProcAsync(
                    connectionFactory, run.ProcName, run.Parameters, cancellationToken);

                if (!run.IsWarmup)
                {
                    runs.Add(new ProcRun(run.ProcName, run.Label, ms, rowCount, run.IsBad));
                }
            }
            catch (Exception ex) when (run.IsWarmup && ex is not OperationCanceledException)
            {
                // Warmup failure is non-fatal — the cache-poisoning call in Scenario 2
                // is best-effort. Log and continue so the real measurements still run.
                logger.LogWarning(ex,
                    "[Scenario {ScenarioId}] Warmup '{ProcName}' failed (non-fatal)",
                    scenario.Id, run.ProcName);
            }
            // Non-warmup exceptions propagate to the global exception handler.
        }

        return BuildScenarioResult(scenario, runs);
    }

    private ScenarioResult BuildScenarioResult(ScenarioDefinition scenario, IReadOnlyList<ProcRun> runs)
    {
        var badMs = runs.Where(r => r.IsBad).Select(r => r.ElapsedMs).DefaultIfEmpty(1).Max();
        var bestMs = runs.Where(r => !r.IsBad).Select(r => r.ElapsedMs).DefaultIfEmpty(1).Min();
        var improvementFactor = badMs > 0 ? Math.Round((double)badMs / Math.Max(bestMs, 1), 1) : 0;

        return new ScenarioResult(
            scenario.Id,
            scenario.Name,
            scenario.Antipattern,
            scenario.Fix,
            runs,
            improvementFactor,
            timeProvider.GetUtcNow());
    }
}
