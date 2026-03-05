using Scalar.AspNetCore;
using SqlDemosApi;

// ============================================================
// SqlDemos API — .NET 10 Minimal API
// ============================================================
// Exposes the bad-vs-fixed stored procedure benchmarks as JSON
// endpoints so they can be called from a browser, Postman, or
// any HTTP client — including other services in Kubernetes.
//
// Local:      dotnet run → http://localhost:5000
// Swagger UI: http://localhost:5000/scalar
// Kubernetes: NodePort 30080 → container 8080
// ============================================================

var builder = WebApplication.CreateBuilder(args);

// ── Connection string ───────────────────────────────────────────────────────
// Reads from appsettings.json for local dev.
// In Kubernetes, env var ConnectionStrings__InterviewDemo overrides it.
// (Double underscore is .NET's nested key separator.)
var connectionString = builder.Configuration.GetConnectionString("InterviewDemo")
    ?? throw new InvalidOperationException(
        "Connection string 'InterviewDemo' not found in appsettings.json " +
        "or ConnectionStrings__InterviewDemo environment variable.");

// ── Services ────────────────────────────────────────────────────────────────
builder.Services.AddSingleton(new BenchmarkService(connectionString));

// OpenAPI spec at /openapi/v1.json — consumed by the Scalar UI
builder.Services.AddOpenApi();

var app = builder.Build();

// ── Middleware ──────────────────────────────────────────────────────────────
app.MapOpenApi();

// Scalar UI — modern OpenAPI explorer (replaces Swashbuckle for .NET 9/10)
// Browse to /scalar after starting the app.
app.MapScalarApiReference(opts =>
{
    opts.Title = ".NET SQL Demos API";
});

// ── Health ──────────────────────────────────────────────────────────────────
app.MapGet("/health", () => Results.Ok(new
{
    status    = "healthy",
    timestamp = DateTimeOffset.UtcNow,
}))
.WithName("HealthCheck")
.WithTags("Health")
.WithSummary("Liveness / readiness probe used by Kubernetes");

// ── Scenarios — list ────────────────────────────────────────────────────────
app.MapGet("/scenarios", () => Results.Ok(new[]
{
    new ScenarioInfo(1, "Cursor Catastrophe",      "Row-by-row cursor loop UPDATE",                         "Set-based UPDATE",                   "usp_Bad_RecalcOrderTotals",          "usp_Fixed_RecalcOrderTotals"),
    new ScenarioInfo(2, "Parameter Sniffing Ghost", "Plan cached for wrong parameter shape",                 "OPTION(RECOMPILE) / OPTIMIZE FOR UNKNOWN", "usp_Bad_GetOrdersByCustomer",   "usp_Fixed_GetOrdersByCustomer"),
    new ScenarioInfo(3, "Non-SARGable Date Trap",  "YEAR()/MONTH() on column prevents index seek",          "Range predicate (>= / <)",           "usp_Bad_GetOrdersByMonth",           "usp_Fixed_GetOrdersByMonth"),
    new ScenarioInfo(4, "SELECT * Flood",           "SELECT * drags in fat NVARCHAR MAX Notes column",       "Explicit column list",               "usp_Bad_GetCustomerOrderHistory",    "usp_Fixed_GetCustomerOrderHistory"),
    new ScenarioInfo(5, "Key Lookup Tax",           "Narrow index forces clustered key lookup per row",      "Covering index with INCLUDE",        "usp_Bad_GetPendingOrders",           "usp_Fixed_GetPendingOrders"),
    new ScenarioInfo(6, "Scalar UDF Killer",        "Scalar UDF in WHERE forces row-by-row serial eval",    "Direct JOIN on TierCode",            "usp_Bad_GetGoldCustomerOrders",      "usp_Fixed_GetGoldCustomerOrders"),
}))
.WithName("ListScenarios")
.WithTags("Scenarios")
.WithSummary("List all 6 benchmark scenarios (no DB calls — metadata only)");

// ── Scenarios — run all ─────────────────────────────────────────────────────
app.MapGet("/scenarios/all", async (BenchmarkService svc) =>
{
    var result = await svc.RunAllAsync();
    return Results.Ok(result);
})
.WithName("RunAllScenarios")
.WithTags("Scenarios")
.WithSummary("Run all 6 bad-vs-fixed stored procedure pairs and return timing results")
.WithDescription(
    "Executes all 6 scenario pairs sequentially. Each pair runs the 'bad' stored procedure " +
    "then the 'fixed' version and returns elapsed milliseconds, row counts, and an improvement factor. " +
    "Scenario 2 (parameter sniffing) includes a cache-poisoning warmup call to demonstrate the problem realistically.");

// ── Scenarios — run one ─────────────────────────────────────────────────────
app.MapGet("/scenarios/{id:int}", async (int id, BenchmarkService svc) =>
{
    if (id is < 1 or > 6)
        return Results.BadRequest(new
        {
            error  = $"Scenario id must be between 1 and 6. Got: {id}.",
            valid  = Enumerable.Range(1, 6),
        });

    var result = await svc.RunScenarioAsync(id);
    return Results.Ok(result);
})
.WithName("RunScenario")
.WithTags("Scenarios")
.WithSummary("Run a single bad-vs-fixed scenario pair (1–6)")
.WithDescription("Valid ids: 1=Cursor, 2=ParameterSniffing, 3=NonSargable, 4=SelectStar, 5=KeyLookup, 6=ScalarUdf");

app.Run();
