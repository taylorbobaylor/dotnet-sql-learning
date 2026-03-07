namespace SqlDemos.Shared;

/// <summary>
/// Describes a single stored-procedure run within a scenario.
/// Warmup runs are executed but excluded from results and comparison output.
/// </summary>
public record ProcRunDefinition(
    string ProcName,
    string Label,
    object? Parameters,
    bool IsBad,
    bool IsWarmup = false);

/// <summary>
/// Data-driven description of one benchmark scenario (bad proc vs fixed proc(s)).
/// Replaces the six near-identical Scenario*Async methods in BenchmarkService and
/// the six Scenario* static functions in the console app.
/// </summary>
public record ScenarioDefinition(
    int Id,
    string Name,
    string Antipattern,
    string Fix,
    IReadOnlyList<ProcRunDefinition> Runs);
