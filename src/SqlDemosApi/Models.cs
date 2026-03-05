namespace SqlDemosApi;

/// <summary>
/// A single stored procedure execution: its name, timing, row count, and whether it's the "bad" or "fixed" version.
/// </summary>
public record ProcRun(
    string Procedure,
    string Label,
    long   ElapsedMs,
    int    RowCount,
    bool   IsBad
);

/// <summary>
/// Full result for one benchmark scenario: bad proc vs fixed proc(s) with improvement stats.
/// </summary>
public record ScenarioResult(
    int                     Id,
    string                  Name,
    string                  Antipattern,
    string                  Fix,
    IReadOnlyList<ProcRun>  Runs,
    double                  ImprovementFactor,
    DateTimeOffset          RanAt
);

/// <summary>
/// Aggregate result returned by GET /scenarios/all.
/// </summary>
public record AllScenariosResult(
    long                        TotalElapsedMs,
    IReadOnlyList<ScenarioResult> Scenarios,
    DateTimeOffset              RanAt
);

/// <summary>
/// Lightweight description of a scenario returned by GET /scenarios (no benchmark data).
/// </summary>
public record ScenarioInfo(
    int    Id,
    string Name,
    string Antipattern,
    string Fix,
    string BadProcedure,
    string FixedProcedure
);
