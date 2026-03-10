namespace SqlDemos.Shared;

/// <summary>
/// Canonical catalog of all benchmark scenarios shared by the console app and the API.
/// Both consumers call <see cref="Build"/> so scenario definitions never diverge.
/// </summary>
public static class ScenarioCatalog
{
    /// <summary>
    /// Total number of benchmark scenarios in the catalog.
    /// Derived from the actual catalog array so it automatically stays in sync when scenarios are added or removed.
    /// </summary>
    public static readonly int Count = Build(DateTime.UtcNow.Year - 1).Length;

    /// <summary>
    /// Builds the full scenario catalog for the given year.
    /// Pass <c>timeProvider.GetUtcNow().Year - 1</c> so Scenario 3's date-range
    /// queries target the previous calendar year.
    /// </summary>
    public static ScenarioDefinition[] Build(int previousYear) =>
    [
        new(
            Id: 1,
            Name: "Cursor Catastrophe",
            Antipattern: "Row-by-row cursor loop UPDATE",
            Fix: "Set-based UPDATE",
            Runs:
            [
                new("dbo.usp_Bad_RecalcOrderTotals",  "Cursor (row-by-row)", new { Status = "Pending" }, IsBad: true),
                new("dbo.usp_Fixed_RecalcOrderTotals", "Set-based UPDATE",   new { Status = "Pending" }, IsBad: false),
            ]),

        new(
            Id: 2,
            Name: "Parameter Sniffing Ghost",
            Antipattern: "Plan compiled for CustomerID=50 (few rows), reused for CustomerID=1 (50k rows)",
            Fix: "OPTION(RECOMPILE) per-call or OPTIMIZE FOR UNKNOWN for high-frequency procs",
            Runs:
            [
                new("dbo.usp_Bad_GetOrdersByCustomer",            "Warmup — CustomerID=50 (poisons plan cache)", new { CustomerID = 50 }, IsBad: false, IsWarmup: true),
                new("dbo.usp_Bad_GetOrdersByCustomer",            "Bad — cached wrong plan (CustomerID=1)",       new { CustomerID = 1 },  IsBad: true),
                new("dbo.usp_Fixed_GetOrdersByCustomer",          "Fixed — OPTION(RECOMPILE)",                    new { CustomerID = 1 },  IsBad: false),
                new("dbo.usp_Fixed_GetOrdersByCustomer_HighFreq", "Fixed — OPTIMIZE FOR UNKNOWN",                 new { CustomerID = 1 },  IsBad: false),
            ]),

        new(
            Id: 3,
            Name: "Non-SARGable Date Trap",
            Antipattern: "YEAR(OrderDate) = @Year — wrapping the column in a function causes a full index scan",
            Fix: "Range predicate: OrderDate >= @Start AND OrderDate < @End — enables index seek",
            Runs:
            [
                new("dbo.usp_Bad_GetOrdersByMonth",   "YEAR()/MONTH() — index scan",  new { Year = previousYear, Month = 6 }, IsBad: true),
                new("dbo.usp_Fixed_GetOrdersByMonth", "Range predicate — index seek", new { Year = previousYear, Month = 6 }, IsBad: false),
            ]),

        new(
            Id: 4,
            Name: "SELECT * Flood",
            Antipattern: "SELECT * drags in the fat Notes (NVARCHAR MAX) column on every row",
            Fix: "Explicit column list — only fetch what the caller actually needs",
            Runs:
            [
                new("dbo.usp_Bad_GetCustomerOrderHistory",   "SELECT * (includes NVARCHAR MAX)", new { CustomerID = 1 }, IsBad: true),
                new("dbo.usp_Fixed_GetCustomerOrderHistory", "Explicit columns",                 new { CustomerID = 1 }, IsBad: false),
            ]),

        new(
            Id: 5,
            Name: "Key Lookup Tax",
            Antipattern: "Narrow index on Status — SQL Server must do a clustered index lookup per matching row",
            Fix: "Covering index with INCLUDE — all needed columns are in the index leaf; no lookups",
            Runs:
            [
                new("dbo.usp_Bad_GetPendingOrders",   "Narrow index — key lookups",      new { Status = "Pending" }, IsBad: true),
                new("dbo.usp_Fixed_GetPendingOrders", "Covering index — no key lookups", new { Status = "Pending" }, IsBad: false),
            ]),

        new(
            Id: 6,
            Name: "Scalar UDF Killer",
            Antipattern: "Scalar UDF in WHERE clause — evaluated row-by-row, prevents parallelism",
            Fix: "Direct JOIN on TierCode — set-based, parallelism allowed",
            Runs:
            [
                new("dbo.usp_Bad_GetGoldCustomerOrders",   "Scalar UDF in WHERE (serial)", new { MinAmount = 500.00m }, IsBad: true),
                new("dbo.usp_Fixed_GetGoldCustomerOrders", "Direct JOIN (parallelism OK)", new { MinAmount = 500.00m }, IsBad: false),
            ]),
    ];
}
