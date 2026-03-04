using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Spectre.Console;
using SqlDemos;

// ============================================================
// SQL Interview Demo Console App
// ============================================================
// Runs each "bad vs fixed" scenario pair and prints a
// side-by-side timing comparison so you can see the
// improvement in real numbers.
//
// Usage:
//   dotnet run              → interactive menu
//   dotnet run -- all       → run all scenarios
//   dotnet run -- 1         → run scenario 1 only
// ============================================================

var config = new ConfigurationBuilder()
    .AddJsonFile("appsettings.json")
    .Build();

var connectionString = config.GetConnectionString("InterviewDemo")
    ?? throw new InvalidOperationException("No connection string found.");

AnsiConsole.Write(new FigletText(".NET SQL Demos").Color(Color.SteelBlue1));
AnsiConsole.MarkupLine("[steelblue1]Interview Prep — Bad vs Fixed Stored Procedures[/]");
AnsiConsole.MarkupLine("[grey]Connect to: localhost,1433 | Database: InterviewDemoDB[/]\n");

// Parse args or show menu
var arg = args.FirstOrDefault()?.ToLower();
var choice = arg switch
{
    "all" => "all",
    "1"   => "1",
    "2"   => "2",
    "3"   => "3",
    "4"   => "4",
    "5"   => "5",
    "6"   => "6",
    _     => null
};

if (choice is null)
{
    choice = AnsiConsole.Prompt(
        new SelectionPrompt<string>()
            .Title("Which scenario would you like to run?")
            .AddChoices([
                "all — Run all 6 scenarios",
                "1 — Cursor Catastrophe (row-by-row vs set-based)",
                "2 — Parameter Sniffing Ghost",
                "3 — Non-SARGable Date Trap",
                "4 — SELECT * Flood",
                "5 — Key Lookup Tax",
                "6 — Scalar UDF Killer",
            ])
    ).Split(' ')[0];
}

var results = new List<BenchmarkResult>();

async Task RunScenario(int n)
{
    Console.WriteLine();
    switch (n)
    {
        case 1: results.AddRange(await Scenario1_CursorVsSetBased(connectionString)); break;
        case 2: results.AddRange(await Scenario2_ParameterSniffing(connectionString)); break;
        case 3: results.AddRange(await Scenario3_NonSargable(connectionString)); break;
        case 4: results.AddRange(await Scenario4_SelectStar(connectionString)); break;
        case 5: results.AddRange(await Scenario5_KeyLookup(connectionString)); break;
        case 6: results.AddRange(await Scenario6_ScalarUdf(connectionString)); break;
    }
}

if (choice == "all")
    for (int i = 1; i <= 6; i++) await RunScenario(i);
else if (int.TryParse(choice, out var n))
    await RunScenario(n);

// Print summary table
if (results.Count > 0)
{
    Console.WriteLine();
    var table = new Table()
        .Border(TableBorder.Rounded)
        .AddColumn("[bold]Scenario[/]")
        .AddColumn("[bold]Stored Procedure[/]")
        .AddColumn("[bold]Rows[/]")
        .AddColumn("[bold]Time (ms)[/]")
        .AddColumn("[bold]Verdict[/]");

    foreach (var r in results)
    {
        var timeColor  = r.IsBad ? "red" : "green";
        var verdict    = r.IsBad ? "[red]✗ Bad[/]" : "[green]✓ Fixed[/]";
        table.AddRow(
            r.ScenarioName,
            $"[grey]{r.SprocName}[/]",
            r.RowCount.ToString("N0"),
            $"[{timeColor}]{r.ElapsedMs} ms[/]",
            verdict
        );
    }

    AnsiConsole.Write(table);
    AnsiConsole.MarkupLine("\n[grey]Tip: Run in SSMS with 'SET STATISTICS IO ON' to see logical reads.[/]");
}

// ============================================================
// SCENARIO 1: Cursor vs Set-Based
// ============================================================
static async Task<List<BenchmarkResult>> Scenario1_CursorVsSetBased(string cs)
{
    PrintScenarioHeader(1, "Cursor Catastrophe", "Row-by-row vs set-based UPDATE");

    AnsiConsole.MarkupLine("[grey]Running bad SP (cursor)...[/]");
    var (badMs, badRows) = await TimeSproc(cs, "dbo.usp_Bad_RecalcOrderTotals",
        new { Status = "Pending" });

    AnsiConsole.MarkupLine("[grey]Running fixed SP (set-based)...[/]");
    var (goodMs, goodRows) = await TimeSproc(cs, "dbo.usp_Fixed_RecalcOrderTotals",
        new { Status = "Pending" });

    PrintComparison("Cursor (row-by-row)", badMs, "Set-based UPDATE", goodMs);

    return
    [
        new(ScenarioName: "1 - Cursor", SprocName: "usp_Bad_RecalcOrderTotals",   ElapsedMs: badMs,  RowCount: badRows,  IsBad: true),
        new(ScenarioName: "1 - Cursor", SprocName: "usp_Fixed_RecalcOrderTotals", ElapsedMs: goodMs, RowCount: goodRows, IsBad: false),
    ];
}

// ============================================================
// SCENARIO 2: Parameter Sniffing
// ============================================================
static async Task<List<BenchmarkResult>> Scenario2_ParameterSniffing(string cs)
{
    PrintScenarioHeader(2, "Parameter Sniffing Ghost",
        "Bad plan for BigCorp (50k orders) when first called for tiny customer");

    // Warm up with a small customer — this poisons the cache for BigCorp
    AnsiConsole.MarkupLine("[grey]First call: CustomerID=50 (few orders) — this warms the cache...[/]");
    await TimeSproc(cs, "dbo.usp_Bad_GetOrdersByCustomer", new { CustomerID = 50 });

    AnsiConsole.MarkupLine("[grey]Second call: CustomerID=1 (BigCorp ~50k orders) — plan from above is REUSED...[/]");
    var (badMs, badRows) = await TimeSproc(cs, "dbo.usp_Bad_GetOrdersByCustomer",
        new { CustomerID = 1 });

    AnsiConsole.MarkupLine("[grey]Fixed SP with OPTION(RECOMPILE): CustomerID=1...[/]");
    var (goodMs, goodRows) = await TimeSproc(cs, "dbo.usp_Fixed_GetOrdersByCustomer",
        new { CustomerID = 1 });

    AnsiConsole.MarkupLine("[grey]Fixed SP with OPTIMIZE FOR UNKNOWN: CustomerID=1...[/]");
    var (unknownMs, _) = await TimeSproc(cs, "dbo.usp_Fixed_GetOrdersByCustomer_HighFreq",
        new { CustomerID = 1 });

    PrintComparison("Bad (cached wrong plan)", badMs, "Fixed (RECOMPILE)", goodMs);
    AnsiConsole.MarkupLine($"[grey]OPTIMIZE FOR UNKNOWN: {unknownMs} ms[/]");

    return
    [
        new("2 - Param Sniff", "usp_Bad_GetOrdersByCustomer",           badMs,    badRows,  true),
        new("2 - Param Sniff", "usp_Fixed_GetOrdersByCustomer",         goodMs,   goodRows, false),
        new("2 - Param Sniff", "usp_Fixed_GetOrdersByCustomer_HighFreq",unknownMs,goodRows, false),
    ];
}

// ============================================================
// SCENARIO 3: Non-SARGable Date Filter
// ============================================================
static async Task<List<BenchmarkResult>> Scenario3_NonSargable(string cs)
{
    PrintScenarioHeader(3, "Non-SARGable Date Trap",
        "YEAR()/MONTH() functions on the column vs range predicate");

    var year = DateTime.UtcNow.Year - 1;
    var month = 6;

    AnsiConsole.MarkupLine($"[grey]Running bad SP: YEAR(OrderDate)={year}, MONTH(OrderDate)={month}...[/]");
    var (badMs, badRows) = await TimeSproc(cs, "dbo.usp_Bad_GetOrdersByMonth",
        new { Year = year, Month = month });

    AnsiConsole.MarkupLine("[grey]Running fixed SP: range predicate...[/]");
    var (goodMs, goodRows) = await TimeSproc(cs, "dbo.usp_Fixed_GetOrdersByMonth",
        new { Year = year, Month = month });

    PrintComparison("YEAR()/MONTH() (index scan)", badMs, "Range predicate (index seek)", goodMs);

    return
    [
        new("3 - SARGable", "usp_Bad_GetOrdersByMonth",   badMs,  badRows,  true),
        new("3 - SARGable", "usp_Fixed_GetOrdersByMonth", goodMs, goodRows, false),
    ];
}

// ============================================================
// SCENARIO 4: SELECT * Flood
// ============================================================
static async Task<List<BenchmarkResult>> Scenario4_SelectStar(string cs)
{
    PrintScenarioHeader(4, "SELECT * Flood",
        "Pulling fat Notes (NVARCHAR MAX) vs explicit column list");

    AnsiConsole.MarkupLine("[grey]Running bad SP (SELECT * including fat Notes column)...[/]");
    var (badMs, badRows) = await TimeSproc(cs, "dbo.usp_Bad_GetCustomerOrderHistory",
        new { CustomerID = 1 });

    AnsiConsole.MarkupLine("[grey]Running fixed SP (explicit columns, no Notes)...[/]");
    var (goodMs, goodRows) = await TimeSproc(cs, "dbo.usp_Fixed_GetCustomerOrderHistory",
        new { CustomerID = 1 });

    PrintComparison("SELECT * (includes NVARCHAR MAX)", badMs, "Explicit columns", goodMs);

    return
    [
        new("4 - SELECT*", "usp_Bad_GetCustomerOrderHistory",   badMs,  badRows,  true),
        new("4 - SELECT*", "usp_Fixed_GetCustomerOrderHistory", goodMs, goodRows, false),
    ];
}

// ============================================================
// SCENARIO 5: Key Lookup Tax
// ============================================================
static async Task<List<BenchmarkResult>> Scenario5_KeyLookup(string cs)
{
    PrintScenarioHeader(5, "Key Lookup Tax",
        "Narrow index (key lookup per row) vs covering index");

    AnsiConsole.MarkupLine("[grey]Running bad SP (narrow index → key lookups)...[/]");
    var (badMs, badRows) = await TimeSproc(cs, "dbo.usp_Bad_GetPendingOrders",
        new { Status = "Pending" });

    AnsiConsole.MarkupLine("[grey]Running fixed SP (covering index — no lookups)...[/]");
    var (goodMs, goodRows) = await TimeSproc(cs, "dbo.usp_Fixed_GetPendingOrders",
        new { Status = "Pending" });

    PrintComparison("Narrow index (key lookups)", badMs, "Covering index (no lookups)", goodMs);

    return
    [
        new("5 - Key Lookup", "usp_Bad_GetPendingOrders",   badMs,  badRows,  true),
        new("5 - Key Lookup", "usp_Fixed_GetPendingOrders", goodMs, goodRows, false),
    ];
}

// ============================================================
// SCENARIO 6: Scalar UDF Killer
// ============================================================
static async Task<List<BenchmarkResult>> Scenario6_ScalarUdf(string cs)
{
    PrintScenarioHeader(6, "Scalar UDF Killer",
        "Scalar UDF in WHERE (row-by-row, serial) vs direct JOIN");

    AnsiConsole.MarkupLine("[grey]Running bad SP (scalar UDF in WHERE)...[/]");
    var (badMs, badRows) = await TimeSproc(cs, "dbo.usp_Bad_GetGoldCustomerOrders",
        new { MinAmount = 500.00m });

    AnsiConsole.MarkupLine("[grey]Running fixed SP (direct JOIN on TierCode)...[/]");
    var (goodMs, goodRows) = await TimeSproc(cs, "dbo.usp_Fixed_GetGoldCustomerOrders",
        new { MinAmount = 500.00m });

    PrintComparison("Scalar UDF (serial, row-by-row)", badMs, "Direct JOIN (parallelism OK)", goodMs);

    return
    [
        new("6 - Scalar UDF", "usp_Bad_GetGoldCustomerOrders",   badMs,  badRows,  true),
        new("6 - Scalar UDF", "usp_Fixed_GetGoldCustomerOrders", goodMs, goodRows, false),
    ];
}

// ============================================================
// Helpers
// ============================================================

static async Task<(long ElapsedMs, int RowCount)> TimeSproc(
    string connectionString, string sprocName, object? parameters = null)
{
    using var connection = new SqlConnection(connectionString);
    await connection.OpenAsync();

    var sw = System.Diagnostics.Stopwatch.StartNew();

    var rows = (await connection.QueryAsync<dynamic>(
        sprocName,
        parameters,
        commandType: CommandType.StoredProcedure,
        commandTimeout: 120)).AsList();

    sw.Stop();
    return (sw.ElapsedMilliseconds, rows.Count);
}

static void PrintScenarioHeader(int num, string name, string description)
{
    var rule = new Spectre.Console.Rule($"[bold steelblue1]Scenario {num}: {name}[/]").RuleStyle("grey");
    AnsiConsole.Write(rule);
    AnsiConsole.MarkupLine($"[grey italic]{description}[/]\n");
}

static void PrintComparison(string badLabel, long badMs, string goodLabel, long goodMs)
{
    var speedup = badMs > 0 ? (double)badMs / Math.Max(goodMs, 1) : 0;
    AnsiConsole.MarkupLine($"  [red]✗ {badLabel}:[/] [bold red]{badMs} ms[/]");
    AnsiConsole.MarkupLine($"  [green]✓ {goodLabel}:[/] [bold green]{goodMs} ms[/]");
    if (speedup > 1)
        AnsiConsole.MarkupLine($"  [yellow]>> {speedup:F1}x faster[/]\n");
}
