using System.Data;
using System.Diagnostics;
using Dapper;
using Microsoft.Data.SqlClient;

namespace SqlDemosApi;

/// <summary>
/// Executes bad-vs-fixed stored procedure pairs and returns structured timing results.
/// Ported from the SqlDemos console app — same procs, same parameters, no console output.
/// </summary>
public class BenchmarkService(string connectionString)
{
    // ──────────────────────────────────────────────────────────────────────────
    // Public API
    // ──────────────────────────────────────────────────────────────────────────

    public async Task<ScenarioResult> RunScenarioAsync(int id) => id switch
    {
        1 => await Scenario1Async(),
        2 => await Scenario2Async(),
        3 => await Scenario3Async(),
        4 => await Scenario4Async(),
        5 => await Scenario5Async(),
        6 => await Scenario6Async(),
        _ => throw new ArgumentOutOfRangeException(nameof(id), $"Scenario id must be 1–6, got {id}.")
    };

    public async Task<AllScenariosResult> RunAllAsync()
    {
        var sw = Stopwatch.StartNew();
        var results = new List<ScenarioResult>();
        for (var i = 1; i <= 6; i++)
            results.Add(await RunScenarioAsync(i));
        sw.Stop();
        return new AllScenariosResult(sw.ElapsedMilliseconds, results, DateTimeOffset.UtcNow);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 1 — Cursor Catastrophe
    // Row-by-row cursor UPDATE vs set-based UPDATE
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<ScenarioResult> Scenario1Async()
    {
        var (badMs,   badRows)   = await TimeProcAsync("dbo.usp_Bad_RecalcOrderTotals",   new { Status = "Pending" });
        var (fixedMs, fixedRows) = await TimeProcAsync("dbo.usp_Fixed_RecalcOrderTotals", new { Status = "Pending" });

        return Build(
            id:          1,
            name:        "Cursor Catastrophe",
            antipattern: "Row-by-row cursor loop UPDATE",
            fix:         "Set-based UPDATE",
            runs:
            [
                new("usp_Bad_RecalcOrderTotals",   "Cursor (row-by-row)",  badMs,   badRows,   IsBad: true),
                new("usp_Fixed_RecalcOrderTotals", "Set-based UPDATE",     fixedMs, fixedRows, IsBad: false),
            ]
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 2 — Parameter Sniffing Ghost
    // Plan cached for a small customer, then reused for BigCorp's 50k orders
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<ScenarioResult> Scenario2Async()
    {
        // Warm up with a tiny customer first — this poisons the plan cache
        await TimeProcAsync("dbo.usp_Bad_GetOrdersByCustomer", new { CustomerID = 50 });

        // Now call with BigCorp (CustomerID=1) — the cached plan is completely wrong
        var (badMs,     badRows)   = await TimeProcAsync("dbo.usp_Bad_GetOrdersByCustomer",           new { CustomerID = 1 });
        var (recompMs,  recompRows)= await TimeProcAsync("dbo.usp_Fixed_GetOrdersByCustomer",         new { CustomerID = 1 });
        var (unknownMs, _)         = await TimeProcAsync("dbo.usp_Fixed_GetOrdersByCustomer_HighFreq", new { CustomerID = 1 });

        return Build(
            id:          2,
            name:        "Parameter Sniffing Ghost",
            antipattern: "Plan compiled for CustomerID=50 (few rows), reused for CustomerID=1 (50k rows)",
            fix:         "OPTION(RECOMPILE) per-call or OPTIMIZE FOR UNKNOWN for high-frequency procs",
            runs:
            [
                new("usp_Bad_GetOrdersByCustomer",            "Bad — cached wrong plan (CustomerID=1)", badMs,     badRows,    IsBad: true),
                new("usp_Fixed_GetOrdersByCustomer",          "Fixed — OPTION(RECOMPILE)",             recompMs,  recompRows, IsBad: false),
                new("usp_Fixed_GetOrdersByCustomer_HighFreq", "Fixed — OPTIMIZE FOR UNKNOWN",          unknownMs, recompRows, IsBad: false),
            ]
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 3 — Non-SARGable Date Trap
    // YEAR()/MONTH() function on the column prevents index seek
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<ScenarioResult> Scenario3Async()
    {
        var year  = DateTime.UtcNow.Year - 1;
        const int month = 6;

        var (badMs,   badRows)   = await TimeProcAsync("dbo.usp_Bad_GetOrdersByMonth",   new { Year = year, Month = month });
        var (fixedMs, fixedRows) = await TimeProcAsync("dbo.usp_Fixed_GetOrdersByMonth", new { Year = year, Month = month });

        return Build(
            id:          3,
            name:        "Non-SARGable Date Trap",
            antipattern: "YEAR(OrderDate) = @Year — wrapping the column in a function causes a full index scan",
            fix:         "Range predicate: OrderDate >= @Start AND OrderDate < @End — enables index seek",
            runs:
            [
                new("usp_Bad_GetOrdersByMonth",   "YEAR()/MONTH() — index scan",  badMs,   badRows,   IsBad: true),
                new("usp_Fixed_GetOrdersByMonth", "Range predicate — index seek", fixedMs, fixedRows, IsBad: false),
            ]
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 4 — SELECT * Flood
    // Pulling fat Notes (NVARCHAR MAX) vs explicit column list
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<ScenarioResult> Scenario4Async()
    {
        var (badMs,   badRows)   = await TimeProcAsync("dbo.usp_Bad_GetCustomerOrderHistory",   new { CustomerID = 1 });
        var (fixedMs, fixedRows) = await TimeProcAsync("dbo.usp_Fixed_GetCustomerOrderHistory", new { CustomerID = 1 });

        return Build(
            id:          4,
            name:        "SELECT * Flood",
            antipattern: "SELECT * drags in the fat Notes (NVARCHAR MAX) column on every row",
            fix:         "Explicit column list — only fetch what the caller actually needs",
            runs:
            [
                new("usp_Bad_GetCustomerOrderHistory",   "SELECT * (includes NVARCHAR MAX)", badMs,   badRows,   IsBad: true),
                new("usp_Fixed_GetCustomerOrderHistory", "Explicit columns",                 fixedMs, fixedRows, IsBad: false),
            ]
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 5 — Key Lookup Tax
    // Narrow nonclustered index forces a clustered index lookup per row
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<ScenarioResult> Scenario5Async()
    {
        var (badMs,   badRows)   = await TimeProcAsync("dbo.usp_Bad_GetPendingOrders",   new { Status = "Pending" });
        var (fixedMs, fixedRows) = await TimeProcAsync("dbo.usp_Fixed_GetPendingOrders", new { Status = "Pending" });

        return Build(
            id:          5,
            name:        "Key Lookup Tax",
            antipattern: "Narrow index on Status — SQL Server must do a clustered index lookup per matching row to fetch remaining columns",
            fix:         "Covering index with INCLUDE — all needed columns are in the index leaf; no lookups",
            runs:
            [
                new("usp_Bad_GetPendingOrders",   "Narrow index → key lookups",       badMs,   badRows,   IsBad: true),
                new("usp_Fixed_GetPendingOrders", "Covering index → no key lookups",  fixedMs, fixedRows, IsBad: false),
            ]
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Scenario 6 — Scalar UDF Killer
    // Scalar UDF in WHERE forces row-by-row serial execution
    // ──────────────────────────────────────────────────────────────────────────
    private async Task<ScenarioResult> Scenario6Async()
    {
        var (badMs,   badRows)   = await TimeProcAsync("dbo.usp_Bad_GetGoldCustomerOrders",   new { MinAmount = 500.00m });
        var (fixedMs, fixedRows) = await TimeProcAsync("dbo.usp_Fixed_GetGoldCustomerOrders", new { MinAmount = 500.00m });

        return Build(
            id:          6,
            name:        "Scalar UDF Killer",
            antipattern: "Scalar UDF in WHERE clause — evaluated row-by-row, prevents parallelism",
            fix:         "Direct JOIN on TierCode — set-based, parallelism allowed",
            runs:
            [
                new("usp_Bad_GetGoldCustomerOrders",   "Scalar UDF in WHERE (serial)",  badMs,   badRows,   IsBad: true),
                new("usp_Fixed_GetGoldCustomerOrders", "Direct JOIN (parallelism OK)",  fixedMs, fixedRows, IsBad: false),
            ]
        );
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────────

    private async Task<(long ElapsedMs, int RowCount)> TimeProcAsync(
        string procName, object? parameters = null)
    {
        using var conn = new SqlConnection(connectionString);
        await conn.OpenAsync();

        var sw = Stopwatch.StartNew();
        var rows = (await conn.QueryAsync<dynamic>(
            procName,
            parameters,
            commandType:    CommandType.StoredProcedure,
            commandTimeout: 120)).AsList();
        sw.Stop();

        return (sw.ElapsedMilliseconds, rows.Count);
    }

    private static ScenarioResult Build(
        int id, string name, string antipattern, string fix,
        IReadOnlyList<ProcRun> runs)
    {
        var badMs  = runs.Where(r => r.IsBad).Select(r => r.ElapsedMs).DefaultIfEmpty(1).Max();
        var bestMs = runs.Where(r => !r.IsBad).Select(r => r.ElapsedMs).DefaultIfEmpty(1).Min();
        var factor = badMs > 0 ? Math.Round((double)badMs / Math.Max(bestMs, 1), 1) : 0;

        return new ScenarioResult(id, name, antipattern, fix, runs, factor, DateTimeOffset.UtcNow);
    }
}
