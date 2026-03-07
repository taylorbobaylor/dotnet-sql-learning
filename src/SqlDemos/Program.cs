using Microsoft.Extensions.Configuration;

using Spectre.Console;

using SqlDemos;
using SqlDemos.Shared;

try
{
    // ── Configuration ─────────────────────────────────────────────────────────────
    var config = new ConfigurationBuilder()
        .AddJsonFile("appsettings.json")
        // Kubernetes: set ConnectionStrings__InterviewDemo in the pod spec to override.
        // Local dev: use dotnet user-secrets or the environment variable.
        .AddEnvironmentVariables()
        .Build();

    var connectionString = config.GetConnectionString("InterviewDemo") is { Length: > 0 } cs
        ? cs
        : throw new InvalidOperationException(
            "Connection string 'InterviewDemo' is missing or empty. " +
            "Set it via dotnet user-secrets or the ConnectionStrings__InterviewDemo environment variable. " +
            "See README.md for setup instructions.");

    IDbConnectionFactory connectionFactory = new SqlConnectionFactory(connectionString);

    // Year - 1 is computed once at startup. Restart the app on New Year's Day if needed.
    var previousYear = TimeProvider.System.GetUtcNow().Year - 1;
    var scenarios = ScenarioCatalog.Build(previousYear);

    // ── Banner + menu ─────────────────────────────────────────────────────────────
    AnsiConsole.Write(new FigletText(".NET SQL Demos").Color(Color.SteelBlue1));
    AnsiConsole.MarkupLine("[steelblue1]Interview Prep — Bad vs Fixed Stored Procedures[/]");
    AnsiConsole.MarkupLine("[grey]Connect to: localhost,1433 | Database: InterviewDemoDB[/]\n");

    var inputArgument = args.FirstOrDefault()?.ToLowerInvariant();
    var choice = inputArgument switch
    {
        "all" => "all",
        "1" or "2" or "3" or "4" or "5" or "6" => inputArgument,
        _ => null,
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

    // ── Run ───────────────────────────────────────────────────────────────────────
    var results = new List<BenchmarkResult>(14);

    if (choice == "all")
    {
        for (int i = 1; i <= scenarios.Length; i++)
        {
            await RunScenarioAsync(i);
        }
    }
    else if (int.TryParse(choice, out var scenarioNumber))
    {
        await RunScenarioAsync(scenarioNumber);
    }

    // ── Summary table ─────────────────────────────────────────────────────────────
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
            var timeColor = r.IsBad ? "red" : "green";
            var verdict = r.IsBad ? "[red]✗ Bad[/]" : "[green]✓ Fixed[/]";
            table.AddRow(
                r.ScenarioName,
                $"[grey]{r.SprocName}[/]",
                r.RowCount.ToString("N0"),
                $"[{timeColor}]{r.ElapsedMs} ms[/]",
                verdict);
        }

        AnsiConsole.Write(table);
        AnsiConsole.MarkupLine("\n[grey]Tip: Run with SET STATISTICS IO ON in VS Code MSSQL or DataGrip to see logical reads.[/]");
    }

    async Task RunScenarioAsync(int scenarioNumber)
    {
        Console.WriteLine();
        try
        {
            results.AddRange(await RunScenarioPairAsync(scenarios[scenarioNumber - 1]));
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Scenario {scenarioNumber} failed — {ex.Message}[/]");
        }
    }

    async Task<List<BenchmarkResult>> RunScenarioPairAsync(
        ScenarioDefinition scenario,
        CancellationToken cancellationToken = default)
    {
        PrintScenarioHeader(scenario.Id, scenario.Name, scenario.Antipattern);

        var scenarioResults = new List<BenchmarkResult>(scenario.Runs.Count);
        long badMs = 0;
        long bestMs = long.MaxValue;
        var badLabel = string.Empty;
        var bestLabel = string.Empty;

        foreach (var run in scenario.Runs)
        {
            AnsiConsole.MarkupLine(run.IsWarmup
                ? $"[grey]Warming up plan cache: {run.ProcName}...[/]"
                : $"[grey]Running {(run.IsBad ? "bad" : "fixed")} SP ({run.Label})...[/]");

            try
            {
                var (ms, rowCount) = await ProcTimer.TimeProcAsync(
                    connectionFactory, run.ProcName, run.Parameters, cancellationToken);

                if (run.IsWarmup)
                {
                    continue;
                }

                scenarioResults.Add(new BenchmarkResult(scenario.Name, run.ProcName, ms, rowCount, run.IsBad));

                if (run.IsBad)
                {
                    badMs = ms;
                    badLabel = run.Label;
                }
                else if (ms < bestMs)
                {
                    bestMs = ms;
                    bestLabel = run.Label;
                }
            }
            catch (Exception ex) when (run.IsWarmup && ex is not OperationCanceledException)
            {
                // Warmup failure is non-fatal — log and continue.
                AnsiConsole.MarkupLine($"[yellow]Warmup failed (non-fatal): {ex.Message}[/]");
            }
        }

        if (bestMs == long.MaxValue)
        {
            bestMs = 0;
        }

        PrintComparison(badLabel, badMs, bestLabel, bestMs);
        return scenarioResults;
    }

    return 0;
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex, ExceptionFormats.ShortenEverything);
    return 1;
}

static void PrintScenarioHeader(int scenarioId, string name, string description)
{
    var rule = new Spectre.Console.Rule($"[bold steelblue1]Scenario {scenarioId}: {name}[/]").RuleStyle("grey");
    AnsiConsole.Write(rule);
    AnsiConsole.MarkupLine($"[grey italic]{description}[/]\n");
}

static void PrintComparison(string badLabel, long badMs, string bestLabel, long bestMs)
{
    var speedup = badMs > 0 ? (double)badMs / Math.Max(bestMs, 1) : 0;
    AnsiConsole.MarkupLine($"  [red]✗ {badLabel}:[/] [bold red]{badMs} ms[/]");
    AnsiConsole.MarkupLine($"  [green]✓ {bestLabel}:[/] [bold green]{bestMs} ms[/]");
    if (speedup > 1)
    {
        AnsiConsole.MarkupLine($"  [yellow]>> {speedup:F1}x faster[/]\n");
    }
}
